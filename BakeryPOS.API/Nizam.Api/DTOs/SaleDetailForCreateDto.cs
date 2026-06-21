using System.ComponentModel.DataAnnotations;

namespace Nizam.Api.DTOs
{
    public class SaleDetailForCreateDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, 1000, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        /// <summary>Selected modifier ids for this line. Empty / null = no modifiers, treated
        /// as legitimate only if every required group on the product also has MinSelect = 0.
        /// Validation happens server-side in <c>ModifierApplicationService</c>.</summary>
        public IReadOnlyList<int>? ModifierIds { get; set; }

        /// <summary>Coursing (dine-in): which course this line belongs to. Defaults to 1.
        /// Ignored by counter-service sales.</summary>
        [Range(1, 20)]
        public int CourseNumber { get; set; } = 1;
    }
}