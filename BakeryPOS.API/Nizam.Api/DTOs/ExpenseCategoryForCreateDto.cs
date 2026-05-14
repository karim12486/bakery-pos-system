using System.ComponentModel.DataAnnotations;
namespace Nizam.Api.DTOs
{
    public class ExpenseCategoryForCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}