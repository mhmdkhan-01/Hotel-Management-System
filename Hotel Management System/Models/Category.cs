// Models/Category.cs
using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_System.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}