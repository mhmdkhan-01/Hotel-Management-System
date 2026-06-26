using Hotel_Management_System.Models;

namespace Hotel_Management_System.ViewModels
{
    public class WaiterDashboardViewModel
    {
        public List<TableStatusDto> AssignedTables { get; set; } = new List<TableStatusDto>();
        public List<OrderItem> PendingDeliveries { get; set; } = new List<OrderItem>();
    }

    public class TableStatusDto
    {
        public int TableId { get; set; }
        public int TableNumber { get; set; }
        public bool IsOccupied { get; set; }
        public SessionStatus? SessionStatus { get; set; }
        public PaymentMethod PreferredPayment { get; set; }
        public decimal TotalBill { get; set; }
    }
}