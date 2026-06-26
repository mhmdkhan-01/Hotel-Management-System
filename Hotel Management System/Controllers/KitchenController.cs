using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Data;
using Hotel_Management_System.Models;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin,Kitchen")]
    public class KitchenController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KitchenController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Live Kitchen Queue Screen
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Gather all items that need preparation, sorted by creation timestamp (oldest first)
            var preparationQueue = await _context.OrderItems
                .Include(oi => oi.MenuItem)
                .Include(oi => oi.Order)
                .ThenInclude(o => o!.Table)
                .Where(oi => (oi.Status == ItemStatus.Ordered || oi.Status == ItemStatus.Cooking)
             && oi.Order.Status == SessionStatus.Active)
                .OrderBy(oi => oi.CreatedAt)
                .ToListAsync();

            return View(preparationQueue);
        }

        // 2. Update Status: Move from Ordered -> Cooking -> Cooked
        [HttpPost]
        public async Task<IActionResult> UpdateItemStatus(int orderItemId, ItemStatus newStatus)
        {
            var item = await _context.OrderItems.FindAsync(orderItemId);
            if (item == null) return NotFound();

            // Guard rails to make sure status transitions stay logical
            if (item.Status != ItemStatus.Cancelled && item.Status != ItemStatus.Delivered)
            {
                item.Status = newStatus;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}