namespace MoneyTracker.Models;

public sealed class MonthlyCategorySummary 
{
    public string CategoryId { get; init; } = "";
    public string CategoryName { get; init; } = "";
    
    public decimal Income { get; init; }
    public decimal Expense { get; init; }

    public decimal Net => Income - Expense;
}