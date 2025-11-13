using System.ComponentModel.DataAnnotations.Schema;

namespace BakeryPOS.API.Core.Entities
{
    public class Expense
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        public int CategoryId { get; set; }
        public ExpenseCategory Category { get; set; }

        // Who recorded this expense
        public int UserId { get; set; }
        public User User { get; set; }
    }
}