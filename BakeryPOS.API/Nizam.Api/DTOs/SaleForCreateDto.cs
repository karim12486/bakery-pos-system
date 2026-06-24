using Nizam.Api.Core.Entities;
using System.ComponentModel.DataAnnotations;
using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs
{
    public class SaleForCreateDto
    {
        // We can add other properties here later, like CustomerId or PaymentType.
        // For now, we only need the list of items.

        [Required]
        [MinLength(1, ErrorMessage = "A sale must contain at least one item.")]
        public ICollection<SaleDetailForCreateDto> SaleDetails { get; set; } = new List<SaleDetailForCreateDto>();

        [Required]
        public PaymentType PaymentMethod { get; set; }

        // The initial amount paid (can be 0 for full credit, or less than total for partial)
        [Range(0, 999999)]
        public decimal AmountPaid { get; set; }

        // The ID of the premium customer, if any
        public int? CustomerId { get; set; }

        public decimal? SplitCashAmount { get; set; }
        public decimal? SplitCardAmount { get; set; }

        /// <summary>Optional promotion coupon code to apply to this sale. The promo discount is
        /// added on top of any customer (Premium) discount.</summary>
        public string? PromoCode { get; set; }

    }
}