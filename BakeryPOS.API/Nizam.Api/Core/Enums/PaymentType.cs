namespace Nizam.Api.Core.Enums
{
    /// <summary>
    /// How a sale was paid. Persisted as a string (see <see cref="Data.AppDbContext.OnModelCreating"/>)
    /// so values are readable in the DB. Renaming members requires a data-migration UPDATE.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: in the cashier UI, the "Credit" button colloquially means *card payment*
    /// (Egyptian usage). That maps to <see cref="Card"/>, NOT to <see cref="Tab"/> below.
    /// See <c>memory/project_credit_means_card.md</c>.
    /// </remarks>
    public enum PaymentType
    {
        /// <summary>Cash tendered at the counter.</summary>
        Cash,

        /// <summary>Bank card via terminal (debit, credit, prepaid — any kind).
        /// The UI's "Credit" button maps to this value.</summary>
        Card,

        /// <summary>Customer tab / shop credit — paid less than total; remainder added to
        /// <c>Customer.CurrentBalance</c> as debt. Settled later via a CustomerPayment.</summary>
        Tab,

        /// <summary>Combination of cash + card on the same sale.</summary>
        Split
    }
}
