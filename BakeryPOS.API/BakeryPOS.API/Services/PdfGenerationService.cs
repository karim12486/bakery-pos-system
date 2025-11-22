using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace BakeryPOS.API.Services
{
    public class PdfGenerationService : IPdfGenerationService
    {
        // We enforce French culture for dates inside the PDF generation explicitly
        private readonly CultureInfo _culture = new CultureInfo("fr-MA");

        public PdfGenerationService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // --- 1. Rapport Journalier (Daily) ---
        public byte[] GenerateDailySalesReport(DailySalesReportDto reportDto)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Text("Rapport Journalier des Ventes").Bold().FontSize(20);
                        // Date format: Jeudi, 22 Novembre 2025
                        row.ConstantItem(150).Text($"{reportDto.ReportDate.ToString("D", _culture)}").AlignRight();
                    });

                    // Content
                    page.Content().Column(col =>
                    {
                        // Summary Cards
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => {
                                    c.Item().Text("Total des Ventes").SemiBold();
                                    c.Item().Text($"{reportDto.GrandTotalSalesValue:C}").FontSize(16);
                                });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => {
                                    c.Item().Text("Transactions").SemiBold();
                                    c.Item().Text($"{reportDto.GrandTotalTransactions}").FontSize(16);
                                });
                        });
                        col.Spacing(20);

                        // --- NEW PAYMENT TABLE ---
                        col.Item().Text("Détails des Paiements").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            // Copy the exact same table definition code from above (it uses the same DTO type)
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); c.ConstantColumn(100); });
                            table.Header(h => {
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Méthode").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Montant").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Trans.").Bold();
                            });

                            foreach (var item in reportDto.PaymentBreakdown)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(TranslatePaymentMethod(item.MethodName));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{item.TotalAmount:C}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{item.TransactionCount}");
                            }
                        });
                        col.Spacing(20);
                        // Sales by Cashier Table
                        col.Item().Text("Performance par Caissier").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); c.ConstantColumn(100); });
                            table.Header(h => {
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Caissier").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Ventes").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Trans.").Bold();
                            });
                            foreach (var item in reportDto.SalesByCashier)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.CashierName);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{item.TotalSalesValue:C}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.TotalTransactions);
                            }
                        });
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); });
                });
            }).GeneratePdf();
        }

        // --- 2. Rapport Mensuel (Monthly) ---
        public byte[] GenerateMonthlySalesReport(MonthlySalesReportDto reportDto)
        {
            // Month Name: e.g., "novembre 2025"
            var monthName = new DateTime(reportDto.Year, reportDto.Month, 1).ToString("MMMM yyyy", _culture);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text($"Rapport Mensuel - {monthName}").Bold().FontSize(20);
                    page.Content().Column(col =>
                    {
                        // Summary Cards
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Total des Ventes").SemiBold(); c.Item().Text($"{reportDto.GrandTotalSalesValue:C}").FontSize(14); });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Transactions").SemiBold(); c.Item().Text($"{reportDto.GrandTotalTransactions}").FontSize(14); });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Panier Moyen").SemiBold(); c.Item().Text($"{reportDto.AverageTransactionValue:C}").FontSize(14); });
                        });
                        col.Spacing(20);
                        // --- NEW PAYMENT TABLE ---
                        col.Item().Text("Répartition des Paiements").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); c.ConstantColumn(100); });

                            table.Header(h => {
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Méthode").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Montant").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Trans.").Bold();
                            });

                            foreach (var item in reportDto.PaymentBreakdown)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(TranslatePaymentMethod(item.MethodName));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{item.TotalAmount:C}");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{item.TransactionCount}");
                            }
                        });
                        // -------------------------

                        col.Spacing(20);
                        // ... Sales by Cashier Table ...
                        // Two columns for details
                        col.Item().Row(row =>
                        {
                            // Left Column: Top Products
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Top 5 Produits").Bold().FontSize(14);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(70); });
                                    table.Header(h => { h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Produit").Bold(); h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Revenu").Bold(); });
                                    foreach (var item in reportDto.TopSellingProducts)
                                    {
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ProductName);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{item.TotalRevenue:C}");
                                    }
                                });
                            });

                            row.Spacing(20);

                            // Right Column: Top Customers
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Top 5 Clients").Bold().FontSize(14);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(70); });
                                    table.Header(h => { h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Client").Bold(); h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Dépensé").Bold(); });
                                    foreach (var item in reportDto.TopCustomers)
                                    {
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.CustomerName);
                                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"{item.TotalSpent:C}");
                                    }
                                });
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); });
                });
            }).GeneratePdf();
        }

        // --- 3. Produits Spéciaux (Special Products) ---
        public byte[] GenerateSpecialProductReport(SpecialProductReportDto reportDto)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text($"Performance Produits Spéciaux - {reportDto.ReportDate.ToString("d", _culture)}").Bold().FontSize(20);
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Product
                            columns.RelativeColumn();  // Added
                            columns.RelativeColumn();  // Sold
                            columns.RelativeColumn();  // Revenue
                            columns.RelativeColumn();  // Profit
                            columns.RelativeColumn();  // Margin
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Produit").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Ajouté").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Vendu").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Revenu").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Bénéfice").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Marge").Bold();
                        });

                        foreach (var product in reportDto.ProductDetails)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(product.ProductName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(product.QuantityAdded);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(product.QuantitySold);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{product.TotalRevenue:C}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{product.Profit:C}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{product.ProfitMargin:F2}%");
                        }
                    });

                    page.Footer().AlignCenter().Text(text => { text.Span("Page "); text.CurrentPageNumber(); });
                });
            }).GeneratePdf();
        }

        private string TranslatePaymentMethod(string method)
        {
            return method switch
            {
                "Cash" => "Espèces",
                "Card" => "Carte Bancaire",
                "Credit" => "Crédit / À Terme",
                _ => method
            };
        }
    }
}