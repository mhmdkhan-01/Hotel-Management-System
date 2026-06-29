using Hotel_Management_System.Data;
using Hotel_Management_System.Hubs;
using Hotel_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_System.Controllers
{
    [Authorize(Roles = "Admin,Kitchen")]
    public class KitchenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public KitchenController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
            if (newStatus == ItemStatus.Cooked)
            {
                await _hubContext.Clients.All.SendAsync("RefreshWaiterFloor");
            }
            return RedirectToAction(nameof(Index));
        }
    }
}