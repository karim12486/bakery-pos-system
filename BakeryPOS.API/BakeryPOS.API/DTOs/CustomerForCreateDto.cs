using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class CustomerForCreateDto
    {
        [Required]
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
    }
}
