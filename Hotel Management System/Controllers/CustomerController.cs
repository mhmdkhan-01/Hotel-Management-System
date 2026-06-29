using Hotel_Management_System.Data;
using Hotel_Management_System.Hubs;
using Hotel_Management_System.Models;
using Hotel_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_System.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly IHubContext<NotificationHub> _hubContext;

        public CustomerController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 1. QR Code Entry point: /Customer/Index?tableNumber=X
        [HttpGet]
        public async Task<IActionResult> Index(int? tableNumber)
        {
            if (tableNumber == null)
            {
                return BadRequest("Invalid Access. Please scan a valid QR Code placed on your table.");
            }

            // Verify table exists in DB, otherwise quickly register it
            var table = await _context.Tables.FirstOrDefaultAsync(t => t.TableNumber == tableNumber);
            if (table == null)
            {
                table = new Table { TableNumber = tableNumber.Value, QrCodeUrl = $"/Customer/Index?tableNumber={tableNumber}" };
                _context.Tables.Add(table);
                await _context.SaveChangesAsync();
            }

            // Find or Create an Active Dining Session for this physical table
            var activeOrder = await _context.Orders
                .FirstOrDefaultAsync(o => o.TableId == table.Id && o.Status == SessionStatus.Active);

            if (activeOrder == null)
            {
                activeOrder = new Order
                {
                    TableId = table.Id,
                    Status = SessionStatus.Active
                };
                _context.Orders.Add(activeOrder);
                table.IsOccupied = true;
                await _context.SaveChangesAsync();
            }

            // Save Table number and Order ID safely into client cookies
            Response.Cookies.Append("TableNumber", tableNumber.ToString()!, new CookieOptions { Expires = DateTimeOffset.Now.AddHours(4) });
            Response.Cookies.Append("ActiveOrderId", activeOrder.Id.ToString(), new CookieOptions { Expires = DateTimeOffset.Now.AddHours(4) });

            return RedirectToAction(nameof(Menu));
        }

        // 2. Interactive Digital Menu
        [HttpGet]
        public async Task<IActionResult> Menu(int? categoryId, string sortOrder)
        {
            if (!Request.Cookies.TryGetValue("TableNumber", out string? tableNumStr) ||
                !Request.Cookies.TryGetValue("ActiveOrderId", out string? orderIdStr))
            {
                return RedirectToAction(nameof(Index), new { tableNumber = 1 }); // Default fallback for safety
            }

            int tableNumber = int.Parse(tableNumStr!);
            int orderId = int.Parse(orderIdStr!);

            var categories = await _context.Categories.ToListAsync();
            var itemsQuery = _context.MenuItems.Where(m => m.IsAvailable).AsQueryable();

            // Filter logic
            if (categoryId.HasValue)
            {
                itemsQuery = itemsQuery.Where(m => m.CategoryId == categoryId.Value);
            }

            // Sort logic
            itemsQuery = sortOrder switch
            {
                "price_low" => itemsQuery.OrderBy(m => m.Price),
                "price_high" => itemsQuery.OrderByDescending(m => m.Price),
                _ => itemsQuery.OrderBy(m => m.Name),
            };

            var menuItems = await itemsQuery.ToListAsync();

            // Get current running items inside this session bill
            var currentItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId && oi.Status != ItemStatus.Cancelled)
                .Include(oi => oi.MenuItem)
                .ToListAsync();

            var currentOrder = await _context.Orders.FindAsync(orderId);

            var viewModel = new CustomerMenuViewModel
            {
                TableNumber = tableNumber,
                ActiveOrderId = orderId,
                Categories = categories,
                MenuItems = menuItems,
                SelectedCategoryId = categoryId,
                SortOrder = sortOrder,
                CurrentSessionItems = currentItems,
                TotalBillSoFar = currentOrder?.TotalBill ?? 0.00m
            };

            return View(viewModel);
        }

        // 3. Handle Add to Order (With Confirmation)
        [HttpPost]
        public async Task<IActionResult> AddToOrder(int menuItemId, int quantity, string customerComment)
        {
            if (!Request.Cookies.TryGetValue("ActiveOrderId", out string? orderIdStr)) return BadRequest();
            int orderId = int.Parse(orderIdStr!);

            var menuItem = await _context.MenuItems.FindAsync(menuItemId);
            if (menuItem == null) return NotFound();

            var orderItem = new OrderItem
            {
                OrderId = orderId,
                MenuItemId = menuItemId,
                Quantity = quantity,
                PriceAtOrder = menuItem.Price,
                CustomerComment = customerComment ?? string.Empty,
                Status = ItemStatus.Pending, // Starts in 30-sec cancel phase
                CreatedAt = DateTime.Now
            };

            _context.OrderItems.Add(orderItem);
            await _context.SaveChangesAsync();

            // Recalculate bill running sums
            await RecalculateBill(orderId);

            await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
            await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
            return RedirectToAction(nameof(OrderStatus));
        }

        // 4. Track Order Status & Handle 30-Sec Cancel Progress bar
        [HttpGet]
        public async Task<IActionResult> OrderStatus()
        {
            if (!Request.Cookies.TryGetValue("ActiveOrderId", out string? orderIdStr))
                return RedirectToAction("Index", "Customer"); // Or wherever you want them to start an order

            int orderId = int.Parse(orderIdStr!);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // 💡 Optional Safety Check: If someone cleared the DB or cookie is old, handle it cleanly
            if (order == null)
            {
                // Option A: Initialize an empty order placeholder so the view still renders nicely
                order = new Hotel_Management_System.Models.Order { SubTotal = 0, OrderItems = new List<OrderItem>() };

                // Option B: Clear the invalid cookie and redirect them back to menu initialization
                // Response.Cookies.Delete("ActiveOrderId");
                // return RedirectToAction("Menu");
            }

            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            if (settings != null)
            {
                decimal cashtax = settings.FixedTaxCashPercent;
                decimal cardtax = settings.FixedTaxCardPercent;

                ViewBag.cashtax = cashtax;
                ViewBag.cardtax = cardtax;

                // Use the safe fallback if order was null
                decimal cashTaxAmount = (order.SubTotal * cashtax) / 100;
                decimal cardTaxAmount = (order.SubTotal * cardtax) / 100;

                ViewBag.cashtaxamount = cashTaxAmount;
                ViewBag.cardtaxamount = cardTaxAmount;
            }

            return View(order);
        }

        // 5. AJAX Cancellation route inside 30 seconds frame
        [HttpPost]
        public async Task<IActionResult> CancelItem(int orderItemId)
        {
            var item = await _context.OrderItems.FindAsync(orderItemId);
            if (item == null) return Json(new { success = false });

            // Ensure cancellation requests only execute if item is strictly within 30 seconds
            if ((DateTime.Now - item.CreatedAt).TotalSeconds <= 30 && item.Status == ItemStatus.Pending)
            {
                item.Status = ItemStatus.Cancelled;
                await _context.SaveChangesAsync();
                await RecalculateBill(item.OrderId);

                await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
                await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
                return RedirectToAction(nameof(Menu));
            }

            return Json(new { success = false, message = "Too late! Item is already sent to the kitchen." });
        }

        // 6. Final Receipt Generation
        [HttpPost]
        public async Task<IActionResult> CompleteSession(PaymentMethod method)
        {
            if (!Request.Cookies.TryGetValue("ActiveOrderId", out string? orderIdStr)) return BadRequest();
            int orderId = int.Parse(orderIdStr!);

            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = SessionStatus.Finished; // Changes state to let Admin know
                order.PreferredPayment = method;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
                await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
            }

            return RedirectToAction(nameof(OrderStatus));
        }

        // Core calculation helper accounting for cash vs card tax parameters set by admin
        private async Task RecalculateBill(int orderId)
        {
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId);
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();

            if (order == null || settings == null) return;

            // Sum only non-cancelled entries
            order.SubTotal = order.OrderItems
                .Where(oi => oi.Status != ItemStatus.Cancelled)
                .Sum(oi => oi.Quantity * oi.PriceAtOrder);

            // Dynamically evaluate tax based on user's current payment selection choice
            decimal taxRate = (order.PreferredPayment == PaymentMethod.Card)
                ? settings.FixedTaxCardPercent
                : settings.FixedTaxCashPercent;

            order.TaxAmount = (order.SubTotal * taxRate) / 100;
            order.TotalBill = order.SubTotal + order.TaxAmount;

            await _context.SaveChangesAsync();
        }
        [HttpPost]
        public async Task<IActionResult> CommitItemToKitchen(int orderItemId)
        {
            var item = await _context.OrderItems
                .Include(oi => oi.Order)
                .FirstOrDefaultAsync(oi => oi.Id == orderItemId);

            if (item != null && item.Status == ItemStatus.Pending)
            {
                // Shifting from Pending (0) to Ordered (1)
                item.Status = ItemStatus.Ordered;

                // Also ensure the main order session is marked Active
                if (item.Order != null && item.Order.Status != SessionStatus.Active)
                {
                    item.Order.Status = SessionStatus.Active;
                }

                await _context.SaveChangesAsync();
                // ... after database _context.SaveChangesAsync() occurs successfully:
                await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
                await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
                await _hubContext.Clients.All.SendAsync("RefreshKitchenDashboard");

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Item not found or already processed." });
        }
    }
}