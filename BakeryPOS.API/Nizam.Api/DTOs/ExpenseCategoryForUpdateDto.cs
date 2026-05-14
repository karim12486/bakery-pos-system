using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs
{
    public class ExpenseCategoryForUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}