using Hotel_Management_System.Data;
using Hotel_Management_System.Hubs;
using Hotel_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin,Waiter")]
    public class WaiterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        public WaiterController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 1. Main Floor Monitor View
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Fetching active orders that need monitoring on the floor
            var activeFloorOrders = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.MenuItem)
                .Where(o => o.Status == SessionStatus.Active || o.Status == SessionStatus.Finished)
                .OrderBy(o => o.Table!.TableNumber)
                .ToListAsync();

            return View(activeFloorOrders);
        }// 2. Mark an individual dish as served to the table
        [HttpPost]
        public async Task<IActionResult> ServeItem(int orderItemId)
        {
            var item = await _context.OrderItems.FindAsync(orderItemId);
            if (item == null) return NotFound();

            // Transition from Cooked (waiting at counter) to Delivered (on table)
            if (item.Status == ItemStatus.Cooked)
            {
                item.Status = ItemStatus.Delivered;
                await _context.SaveChangesAsync();
            }
            await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
            await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
            return RedirectToAction(nameof(Index));
        }

        // 3. Acknowledge and clear a table's payment alert
        [HttpPost]
        public async Task<IActionResult> ClearPaymentAlert(int orderId)
        {
            var order = await _context.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == orderId);

            if (order != null && order.Status == SessionStatus.Finished)
            {
                // Set to Completed, closing the loop and freeing up the table
                order.Status = SessionStatus.Completed;
                order.SessionEnd = DateTime.Now;

                if (order.Table != null)
                {
                    order.Table.IsOccupied = false;
                }

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
                await _hubContext.Clients.All.SendAsync("RefreshAdminDashboard");
            }

            return RedirectToAction(nameof(Index));
        }
    }
}