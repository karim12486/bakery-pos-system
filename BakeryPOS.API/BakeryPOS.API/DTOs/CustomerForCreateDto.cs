using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class CustomerForCreateDto
    {
        [Required]
        public string Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        [Range(0, 100)]
        public decimal DiscountPercentage { get; set; } = 0;
        public string? ImageUrl { get; set; }
        public decimal CurrentBalance { get; set; }
    }
}
