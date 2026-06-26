// Models/TableAssignment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public class TableAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TableId { get; set; }

        [ForeignKey("TableId")]
        public virtual Table? Table { get; set; }

        [Required]
        public string WaiterId { get; set; } = string.Empty; // Maps to ApplicationUser (Identity)
    }
}