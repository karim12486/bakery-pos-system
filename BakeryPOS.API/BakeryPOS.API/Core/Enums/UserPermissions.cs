namespace BakeryPOS.API.Core.Enums
{
    [Flags]
    public enum UserPermissions
    {
        None = 0,

        // 1. The POS Screen (SalesController)
        // Allows opening the register, scanning items, and taking money.
        ProcessSales = 1 << 0,

        // 2. Security Action (RemovalController / SignalR)
        // Allows the user to approve item deletions on the cashier screen.
        ApproveRemovals = 1 << 1,

        // 3. Product Catalog (ProductsController & CategoriesController)
        // Full access to Create, Edit, Delete Products and Categories.
        ManageProducts = 1 << 2,

        // 4. Stock Management (InventoryController)
        // Access to add stock and view stock history.
        ManageInventory = 1 << 3,

        // 5. Client Management (CustomersController)
        // Create/Edit Customers and record credit payments.
        ManageCustomers = 1 << 4,

        // 6. Expense Tracking (ExpensesController)
        // Record and categorize operational costs.
        ManageExpenses = 1 << 5,

        // 7. Staff Management (AdminController)
        // Create users, reset passwords, change permissions.
        ManageUsers = 1 << 6,

        // 8. Analytics (DashboardController & ReportsController)
        // View the dashboard stats, charts, and download PDF reports.
        AccessReports = 1 << 7,

        // --- Roles ---

        // Admin has access to EVERYTHING
        Admin = 255,

        // Standard Cashier: Can sell items and manage customers (for credit), but nothing else.
        Cashier = ProcessSales | ManageCustomers
    }
}