using System.ComponentModel.DataAnnotations;

namespace BakeryPOS.API.DTOs
{
    public class RemovalRequestCreateDto
    {
        [Required]
        public string ProductName { get; set; }

        [Required]
        [Range(0, 9999)]
        public decimal ProductPrice { get; set; }

        // The frontend will get this from its SignalR connection and send it with the request
        [Required]
        public string CashierConnectionId { get; set; }
    }
}