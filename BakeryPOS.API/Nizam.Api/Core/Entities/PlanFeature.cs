namespace Nizam.Api.Core.Entities;

/// <summary>
/// A single feature flag granted by a plan. Many-to-many on the conceptual level (Plan ↔ Feature),
/// but feature definitions are implicit — the string key is the contract between backend code
/// (which checks <c>HasFeature("modifiers")</c>) and the seed data.
///
/// Known feature keys (Phase A + Phase B):
/// <list type="bullet">
///   <item><c>multi_branch</c> — more than 1 branch allowed (limit governs the count)</item>
///   <item><c>modifiers</c> — modifier groups on products</item>
///   <item><c>promotions</c> — promotions/discounts engine</item>
///   <item><c>loyalty</c> — loyalty points earn/redeem</item>
///   <item><c>customer_premium</c> — Premium customer % discount</item>
///   <item><c>scheduled_reports</c> — daily/weekly Hangfire-driven reports</item>
///   <item><c>custom_receipt_branding</c> — tenant logo + business info on receipt</item>
///   <item><c>white_label</c> — no NIZAM footer on receipt (Pro only)</item>
///   <item><c>tables</c> — areas + tables + floor plan (Phase B / Pro)</item>
///   <item><c>kds</c> — kitchen display system (Phase B / Pro)</item>
///   <item><c>split_check</c> — multi-check bills (Phase B / Pro)</item>
///   <item><c>reservations</c> — table reservations (Phase B / Pro)</item>
///   <item><c>api_access</c> — public REST API + webhooks (Pro)</item>
///   <item><c>inventory_ops</c> — purchase orders, recipes, COGS, transfers (Pro)</item>
///   <item><c>whatsapp_notifications</c> — per-tenant WhatsApp (Pro / Growth add-on)</item>
///   <item><c>qr_table_menu</c> — public QR-code customer menu</item>
///   <item><c>ai_insights</c> — NIZAM Insights add-on (RAG chatbot)</item>
/// </list>
/// </summary>
public class PlanFeature
{
    public int Id { get; set; }

    /// <summary>FK → <see cref="Plan.Code"/>.</summary>
    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Stable string key checked in code via <c>HasFeature(key)</c>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    public Plan? Plan { get; set; }
}
