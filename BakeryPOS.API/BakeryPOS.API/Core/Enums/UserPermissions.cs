namespace BakeryPOS.API.Core.Enums
{
    [Flags]
    public enum UserPermissions
    {
        None = 0,
        AllowReturns = 1 << 0,                  // 1 (Retour)
        ApplyDiscounts = 1 << 1,                // 2 (Remise)
        ModifyPrice = 1 << 2,                   // 4 (ModifPrix)
        ModifyQuantity = 1 << 3,                // 8 (ModifQte)
        ApplyHalfPrice = 1 << 4,                // 16 (PrixDemi)
        UseMiscellaneous = 1 << 5,              // 32 (Divers)
        PrintDuplicateReceipt = 1 << 6,         // 64 (Duplicata)
        UseFlashSale = 1 << 7,                  // 128 (Flash - Quick sale item)
        OpenCashDrawer = 1 << 8,                // 256 (Tiroir)
        CancelSale = 1 << 9,                    // 512 (Annulation)
        ViewReports = 1 << 10,                  // 1024 (Rapport)
        ManageExpenses = 1 << 11,               // 2048 (Depenses)
        ManageStockIn = 1 << 12,                // 4096 (Receptions)
        ManageStockOut = 1 << 13,               // 8192 (Sorties)
        ManageCustomers = 1 << 14,              // 16384 (Clients)
        ProcessCustomerPayments = 1 << 15,      // 32768 (ClientsRegle)
        ManageProducts = 1 << 16,               // 65536 (Articles)
        AddToBill = 1 << 17,                    // 131072 (Addition)
        FinalizeSale = 1 << 18,                 // 262144 (Solder)
        PerformDirectSale = 1 << 19,            // 524288 (Directe)
        PerformEndOfDayClosure = 1 << 20,       // 1048576 (Cloture)

        // --- Example Role Combinations ---

        // A user with ALL permissions
        Admin = ~None,

        // A standard cashier role
        Cashier = ApplyDiscounts | OpenCashDrawer | CancelSale | AddToBill | FinalizeSale | ManageCustomers
    }
}