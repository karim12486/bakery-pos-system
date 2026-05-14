using System.ComponentModel.DataAnnotations;
namespace Nizam.Api.DTOs
{
    public class CategoryForCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}