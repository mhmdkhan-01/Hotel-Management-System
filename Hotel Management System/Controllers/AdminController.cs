using Hotel_Management_System.Data;
using Hotel_Management_System.Models;
using Hotel_Management_System.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager; // Or IdentityUser depending on your Setup
        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Core Analytics Dashboard Home View
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;

            // Gather Analytics Metrics
            var ordersTodayQuery = _context.Orders
                .Where(o => o.SessionStart >= today && o.Status == SessionStatus.Completed);

            decimal earnings = await ordersTodayQuery.SumAsync(o => o.TotalBill);
            int orderCount = await ordersTodayQuery.CountAsync();

            int itemsCount = await _context.OrderItems
                .Where(oi => oi.CreatedAt >= today && oi.Status == ItemStatus.Delivered)
                .SumAsync(oi => oi.Quantity);

            // Fetch live ongoing table sessions (Active or waiting for checkout receipt)
            var activeOrders = await _context.Orders
                .Where(o => o.Status != SessionStatus.Completed)
                .Include(o => o.Table)
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.SessionStart)
                .ToListAsync();

            // Simple Grocery/Inventory tracker mapping out top used menu items
            var topItems = await _context.OrderItems
                .Where(oi => oi.Status == ItemStatus.Delivered)
                .GroupBy(oi => oi.MenuItem!.Name)
                .Select(g => new TopSoldItemDto
                {
                    ItemName = g.Key,
                    TotalQuantity = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Quantity * oi.PriceAtOrder)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(5)
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalEarningsToday = earnings,
                TotalOrdersToday = orderCount,
                TotalItemsSoldToday = itemsCount,
                RecentActiveOrders = activeOrders,
                TopSoldItems = topItems
            };

            return View(viewModel);
        }

        // 2. Action method to let Admin close a table's session once bill is paid
        [HttpPost]
        public async Task<IActionResult> CloseTableSession(int orderId)
        {
            var order = await _context.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order != null)
            {
                order.Status = SessionStatus.Completed;
                order.SessionEnd = DateTime.Now;

                if (order.Table != null)
                {
                    order.Table.IsOccupied = false;
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Dashboard));
        }
        // --- MENU ITEM MANAGEMENT ---

        // 1. Display list of all menu dishes
        [HttpGet]
        public async Task<IActionResult> ManageItems()
        {
            var items = await _context.MenuItems.Include(m => m.Category).ToListAsync();
            ViewBag.Categories = await _context.Categories.ToListAsync(); // For the "Add Item" dropdown modal
            return View(items);
        }

        // 2. Create a new dish
       
    [HttpPost]
    public async Task<IActionResult> CreateItem(MenuItem item, string? NewCategoryName, IFormFile? ImageFile)
    {
        // 1. Dynamic Category Interception
        if (!string.IsNullOrWhiteSpace(NewCategoryName))
        {
            // Check if it already exists to prevent duplication error states
            var existingCat = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == NewCategoryName.Trim().ToLower());

            if (existingCat != null)
            {
                item.CategoryId = existingCat.Id;
            }
            else
            {
                var newCat = new Category { Name = NewCategoryName.Trim() };
                _context.Categories.Add(newCat);
                await _context.SaveChangesAsync(); // Saves first to generate the foreign key ID assignment
                item.CategoryId = newCat.Id;
            }
        }

        // 2. Local Device Image File Processing
        if (ImageFile != null && ImageFile.Length > 0)
        {
            // Define destination file target path inside wwwroot/images/
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Generate clean file name format to prevent collision risks
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ImageFile.FileName);
            string filePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await ImageFile.CopyToAsync(stream);
            }

            item.ImageUrl = "/images/" + uniqueFileName;
        }
        else if (string.IsNullOrEmpty(item.ImageUrl))
        {
            item.ImageUrl = "/images/default-food.png";
        }

        // Clear validation discrepancies caused by manually overriding relational fields
        ModelState.Remove("Category");
        ModelState.Remove("NewCategoryName");
            ModelState.Remove("CategoryId");
            if (ModelState.IsValid)
        {
            item.IsAvailable = true;
            _context.MenuItems.Add(item);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ManageItems));
    }

    // 3. Toggle Availability (Instantly reflects on customer phone)
    [HttpPost]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item != null)
            {
                item.IsAvailable = !item.IsAvailable;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(ManageItems));
        }


        // --- WAITER MANAGEMENT ---

        // 4. View all registered Waiter Staff accounts
        [HttpGet]
        public async Task<IActionResult> ManageWaiters()
        {
            // Fetch users belonging to the "Waiter" role via Identity
            var waiters = await _context.Users
                .Where(u => _context.UserRoles
                    .Any(ur => ur.UserId == u.Id &&
                               _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Waiter")))
                .ToListAsync();

            return View(waiters);
        }
        [HttpPost]
        public async Task<IActionResult> CreateWaiter(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return RedirectToAction(nameof(ManageWaiters));
            }

            var newWaiter = new ApplicationUser // Use IdentityUser if you didn't customize it
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(newWaiter, password);
            if (result.Succeeded)
            {
                // Explicitly assign them to the "Waiter" security role context
                await _userManager.AddToRoleAsync(newWaiter, "Waiter");
            }

            return RedirectToAction(nameof(ManageWaiters));
        }

        // 2. Remove/Delete a Waiter Account
        [HttpPost]
        public async Task<IActionResult> DeleteWaiter(string id)
        {
            var waiter = await _userManager.FindByIdAsync(id);
            if (waiter != null)
            {
                await _userManager.DeleteAsync(waiter);
            }
            return RedirectToAction(nameof(ManageWaiters));
        }
    }
}