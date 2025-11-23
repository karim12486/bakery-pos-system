namespace BakeryPOS.API.Core.Enums
{
    public enum PaymentType
    {
        Cash,
        Card,
        Credit, // The customer is paying later
        Split // Combination of multiple payment types
    }
}
