// Models/Order.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public enum SessionStatus
    {
        Active,      // Customer is still ordering items one by one
        Finished,    // Customer pressed "Finished", waiting to pay final bill
        Completed    // Bill paid, admin/waiter closed the table session
    }

    public enum PaymentMethod
    {
        None,
        Cash,
        Card
    }

    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TableId { get; set; }

        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }

        public DateTime SessionStart { get; set; } = DateTime.Now;
        public DateTime? SessionEnd { get; set; }

        [Required]
        public SessionStatus Status { get; set; } = SessionStatus.Active;

        [Required]
        public PaymentMethod PreferredPayment { get; set; } = PaymentMethod.None;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0.00m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalBill { get; set; } = 0.00m;

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}