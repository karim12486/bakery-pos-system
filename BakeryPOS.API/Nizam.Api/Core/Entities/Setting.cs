namespace Nizam.Api.Core.Entities;

/// <summary>
/// Key/value tenant-scoped configuration. Examples: <c>business.name</c>, <c>tax.rate</c>,
/// <c>currency.code</c>, <c>receipt.header</c>, <c>locale.default</c>.
///
/// Strings only — typed accessors live on top in <c>ISettingsService</c>. Keeping the
/// schema simple means adding a new setting key is a one-line code change, no migration.
/// </summary>
public class Setting
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    /// <summary>Dotted key e.g. <c>tax.rate</c>, <c>business.name</c>. Unique per tenant.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Value as string. Typed accessors convert on read.</summary>
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
