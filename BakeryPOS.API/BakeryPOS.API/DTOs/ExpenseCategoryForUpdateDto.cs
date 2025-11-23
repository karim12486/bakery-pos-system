using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class ExpenseCategoryForUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}