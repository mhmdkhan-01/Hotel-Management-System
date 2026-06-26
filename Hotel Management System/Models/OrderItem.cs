// Models/OrderItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public enum ItemStatus
    {
        Pending,     // Inside the 30-second cancellation window
        Ordered,     // Confirmed, appears on Kitchen Desktop Screen
        Cooking,     // Kitchen marked as actively cooking
        Cooked,      // Ready for waiter delivery
        Delivered,   // Waiter delivered to table
        Cancelled    // Cancelled by customer within 30s
    }

    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem? MenuItem { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAtOrder { get; set; }

        [StringLength(255)]
        public string CustomerComment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        public ItemStatus Status { get; set; } = ItemStatus.Pending;
    }
}