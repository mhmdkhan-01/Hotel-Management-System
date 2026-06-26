using Hotel_Management_System.Models;

namespace Hotel_Management_System.ViewModels
{
    public class AdminDashboardViewModel
    {
        public decimal TotalEarningsToday { get; set; }
        public int TotalOrdersToday { get; set; }
        public int TotalItemsSoldToday { get; set; }
        public List<Order> RecentActiveOrders { get; set; } = new List<Order>();
        public List<TopSoldItemDto> TopSoldItems { get; set; } = new List<TopSoldItemDto>();
    }

    public class TopSoldItemDto
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}