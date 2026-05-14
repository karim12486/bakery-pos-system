using Nizam.Api.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nizam.Api.Data.Seed;

/// <summary>
/// Idempotent upsert of the subscription plan catalog (Plans + PlanFeatures + PlanLimits).
/// Runs on every startup so plan content stays in sync with code — the migration only
/// guarantees the rows EXIST so the FK can be enforced; this seeder is the source of truth
/// for what each plan grants.
///
/// Adding a new plan: append a tuple to <see cref="PlanCatalog"/>.
/// Adding a feature/limit to an existing plan: edit the tuple. Existing tenants are unaffected
/// (their PlanCode FK doesn't change) but immediately inherit the new feature/limit.
///
/// REMOVING a feature/limit: deliberately not handled here — that's a breaking change for
/// existing tenants and requires a real migration / customer comms. Add a stub here that
/// throws if a stale feature row is found, so we don't silently revoke entitlements.
/// </summary>
public static class PlanCatalogSeeder
{
    /// <summary>The authoritative plan catalog. Source of truth for every plan's pricing,
    /// feature flags, and limits. Matches docs/planning/2026-05-14-subscription-tiers.md.</summary>
    private static readonly PlanDefinition[] PlanCatalog =
    {
        new(
            Code: "starter",
            Name: "Starter",
            Description: "Single-branch POS for kiosks, juice bars, and small shops.",
            MonthlyPriceEgp: 999m,
            AnnualPriceEgp: 9_990m,
            SortOrder: 10,
            Features: Array.Empty<string>(),
            Limits: new (string, int)[]
            {
                ("max_branches", 1),
                ("max_users", 3),
                ("max_products", 50),
                ("audit_log_retention_days", 7),
            }
        ),
        new(
            Code: "growth",
            Name: "Growth",
            Description: "Multi-branch café/bakery with modifiers, loyalty, and promotions.",
            MonthlyPriceEgp: 1_999m,
            AnnualPriceEgp: 19_990m,
            SortOrder: 20,
            Features: new[]
            {
                "multi_branch",
                "modifiers",
                "promotions",
                "customer_premium",
                "loyalty",
                "scheduled_reports",
                "custom_receipt_branding",
            },
            Limits: new (string, int)[]
            {
                ("max_branches", 3),
                ("max_users", 15),
                ("max_products", 200),
                ("audit_log_retention_days", 90),
            }
        ),
        new(
            Code: "pro",
            Name: "Pro",
            Description: "Restaurants, KDS, tables, full inventory ops, public API, white-label.",
            MonthlyPriceEgp: 3_499m,
            AnnualPriceEgp: 34_990m,
            SortOrder: 30,
            Features: new[]
            {
                "multi_branch",
                "modifiers",
                "promotions",
                "customer_premium",
                "loyalty",
                "scheduled_reports",
                "custom_receipt_branding",
                "white_label",
                "tables",
                "kds",
                "split_check",
                "reservations",
                "qr_table_menu",
                "api_access",
                "inventory_ops",
                "whatsapp_notifications",
            },
            Limits: new (string, int)[]
            {
                ("max_branches", 10),
                ("max_users", -1),                  // unlimited
                ("max_products", -1),               // unlimited
                ("audit_log_retention_days", 365),
            }
        ),
    };

    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        foreach (var def in PlanCatalog)
        {
            // ---- Plan row ----
            var plan = await context.Plans.FirstOrDefaultAsync(p => p.Code == def.Code, ct);
            if (plan == null)
            {
                plan = new Plan
                {
                    Code = def.Code,
                    Name = def.Name,
                    Description = def.Description,
                    MonthlyPriceEgp = def.MonthlyPriceEgp,
                    AnnualPriceEgp = def.AnnualPriceEgp,
                    SortOrder = def.SortOrder,
                    IsActive = true,
                    IsPubliclyVisible = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                context.Plans.Add(plan);
                logger.LogInformation("Seeded Plan code={Code}", def.Code);
            }
            else
            {
                // Keep pricing/description in sync with code.
                var changed = false;
                if (plan.Name != def.Name) { plan.Name = def.Name; changed = true; }
                if (plan.Description != def.Description) { plan.Description = def.Description; changed = true; }
                if (plan.MonthlyPriceEgp != def.MonthlyPriceEgp) { plan.MonthlyPriceEgp = def.MonthlyPriceEgp; changed = true; }
                if (plan.AnnualPriceEgp != def.AnnualPriceEgp) { plan.AnnualPriceEgp = def.AnnualPriceEgp; changed = true; }
                if (plan.SortOrder != def.SortOrder) { plan.SortOrder = def.SortOrder; changed = true; }
                if (changed) { plan.UpdatedAt = now; logger.LogInformation("Updated Plan code={Code}", def.Code); }
            }

            // ---- PlanFeatures (add any missing; never remove — that's a breaking change) ----
            var existingFeatures = await context.PlanFeatures
                .Where(f => f.PlanCode == def.Code)
                .Select(f => f.FeatureKey)
                .ToListAsync(ct);

            foreach (var featureKey in def.Features)
            {
                if (existingFeatures.Contains(featureKey)) continue;
                context.PlanFeatures.Add(new PlanFeature { PlanCode = def.Code, FeatureKey = featureKey });
                logger.LogInformation("Granted feature {Feature} to plan {Code}", featureKey, def.Code);
            }

            // ---- PlanLimits (upsert) ----
            var existingLimits = await context.PlanLimits
                .Where(l => l.PlanCode == def.Code)
                .ToListAsync(ct);

            foreach (var (limitKey, value) in def.Limits)
            {
                var existing = existingLimits.FirstOrDefault(l => l.LimitKey == limitKey);
                if (existing == null)
                {
                    context.PlanLimits.Add(new PlanLimit { PlanCode = def.Code, LimitKey = limitKey, Value = value });
                    logger.LogInformation("Set limit {Key}={Value} on plan {Code}", limitKey, value, def.Code);
                }
                else if (existing.Value != value)
                {
                    existing.Value = value;
                    logger.LogInformation("Updated limit {Key}={Value} on plan {Code}", limitKey, value, def.Code);
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private record PlanDefinition(
        string Code,
        string Name,
        string Description,
        decimal MonthlyPriceEgp,
        decimal AnnualPriceEgp,
        int SortOrder,
        string[] Features,
        (string Key, int Value)[] Limits);
}
