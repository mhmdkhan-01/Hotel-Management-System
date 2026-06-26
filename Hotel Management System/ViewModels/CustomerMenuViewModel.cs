using Hotel_Management_System.Models;

namespace Hotel_Management_System.ViewModels
{
    public class CustomerMenuViewModel
    {
        public int TableNumber { get; set; }
        public int ActiveOrderId { get; set; }
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public int? SelectedCategoryId { get; set; }
        public string SortOrder { get; set; } = string.Empty;
        public List<OrderItem> CurrentSessionItems { get; set; } = new List<OrderItem>();
        public decimal TotalBillSoFar { get; set; }
    }
}