// Models/Table.cs
using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_System.Models
{
    public class Table
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TableNumber { get; set; }

        [Required]
        [StringLength(255)]
        public string QrCodeUrl { get; set; } = string.Empty;

        public bool IsOccupied { get; set; } = false;

        public virtual ICollection<TableAssignment> TableAssignments { get; set; } = new List<TableAssignment>();
    }
}