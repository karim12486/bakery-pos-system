using System.ComponentModel.DataAnnotations;
namespace BakeryPOS.API.DTOs
{
    public class ExpenseForCreateDto
    {
        [Required]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, 999999)]
        public decimal Amount { get; set; }

        [Required]
        public int CategoryId { get; set; }
    }
}