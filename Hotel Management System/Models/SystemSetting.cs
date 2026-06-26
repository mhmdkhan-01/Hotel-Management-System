using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_System.Models
{
    public class SystemSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string HotelName { get; set; } = "Our Restaurant";

        [Required]
        public decimal FixedTaxCashPercent { get; set; }

        [Required]
        public decimal FixedTaxCardPercent { get; set; }
    }
}