using System.ComponentModel.DataAnnotations;
namespace BakeryPOS.API.DTOs
{
    public class ExpenseCategoryForCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}