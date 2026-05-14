namespace Nizam.Api.Core.Entities
{
    public class ExpenseCategory
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}