using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BakeryPOS.API.Services
{
    public class PdfGenerationService : IPdfGenerationService
    {
        public PdfGenerationService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // --- 1. Daily Sales Report (Enhanced) ---
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
                        row.RelativeItem().Text($"Daily Sales Report").Bold().FontSize(20);
                        row.ConstantItem(100).Text($"{reportDto.ReportDate:yyyy-MM-dd}").AlignRight();
                    });

                    // Content
                    page.Content().Column(col =>
                    {
                        // Summary Cards
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => {
                                    c.Item().Text("Total Sales").SemiBold();
                                    c.Item().Text($"{reportDto.GrandTotalSalesValue:C}").FontSize(16);
                                });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => {
                                    c.Item().Text("Total Transactions").SemiBold();
                                    c.Item().Text($"{reportDto.GrandTotalTransactions}").FontSize(16);
                                });
                        });
                        col.Spacing(20);

                        // Sales by Cashier Table
                        col.Item().Text("Sales by Cashier").Bold().FontSize(14);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(100); c.ConstantColumn(100); });
                            table.Header(h => {
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Cashier").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total Sales").Bold();
                                h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Transactions").Bold();
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

        // --- 2. Monthly Sales Report (Enhanced) ---
        public byte[] GenerateMonthlySalesReport(MonthlySalesReportDto reportDto)
        {
            var monthName = new DateTime(reportDto.Year, reportDto.Month, 1).ToString("MMMM yyyy");
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text($"Monthly Sales Report - {monthName}").Bold().FontSize(20);
                    page.Content().Column(col =>
                    {
                        // Summary Cards
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Total Sales").SemiBold(); c.Item().Text($"{reportDto.GrandTotalSalesValue:C}").FontSize(14); });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Total Transactions").SemiBold(); c.Item().Text($"{reportDto.GrandTotalTransactions}").FontSize(14); });
                            row.Spacing(10);
                            row.RelativeItem().Border(1).Background(Colors.Grey.Lighten4).Padding(5)
                                .Column(c => { c.Item().Text("Avg. Transaction").SemiBold(); c.Item().Text($"{reportDto.AverageTransactionValue:C}").FontSize(14); });
                        });
                        col.Spacing(20);

                        // Two columns for details
                        col.Item().Row(row =>
                        {
                            // Left Column: Top Products
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Top 5 Selling Products").Bold().FontSize(14);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(70); });
                                    table.Header(h => { h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product").Bold(); h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Revenue").Bold(); });
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
                                c.Item().Text("Top 5 Customers").Bold().FontSize(14);
                                c.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(70); });
                                    table.Header(h => { h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Customer").Bold(); h.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total Spent").Bold(); });
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

        // --- 3. Special Product Report (Enhanced) ---
        public byte[] GenerateSpecialProductReport(SpecialProductReportDto reportDto)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text($"Special Product Performance - {reportDto.ReportDate:yyyy-MM-dd}").Bold().FontSize(20);
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
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Added").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Sold").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Revenue").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Profit").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).AlignRight().Text("Margin").Bold();
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
    }
}