using System.ComponentModel.DataAnnotations;
namespace BakeryPOS.API.DTOs
{
    public class CategoryForCreateDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }
}