using Dapper;
using MoneyTracker.Models;

namespace MoneyTracker.Services;

public sealed class TransactionService
{
    public IReadOnlyList<TransactionRow> GetAll()
    {
        using var conn = Database.OpenConnection();
        conn.Open();

        var rows = conn.Query("""
                              SELECT Id, Date, CategoryId, Name, CostCents, Type
                              FROM Transactions
                              ORDER BY Date DESC, Name ASC;
                              """).ToList();

        return rows.Select(r => new TransactionRow()
        {
            Id = (string)r.Id,
            Date = UnixToLocalDate((long)r.Date),
            CategoryId = (string)r.CategoryId,
            Name = (string)r.Name,
            Cost = CentsToDollars((long)r.CostCents),
            Type = (TransactionType)(long)r.Type,
        }).ToList();
    }

    public void Upsert(TransactionRow row)
    {
        using var conn = Database.OpenConnection();
        conn.Open();

        // Ensure Id exists (defensive)
        if (string.IsNullOrWhiteSpace(row.Id))
            row.Id = Guid.NewGuid().ToString();

        var dateUnix = LocalDateToUnixMidnight(row.Date);
        var costCents = DollarsToCents(row.Cost);

        conn.Execute("""
                         INSERT INTO Transactions (Id, Date, CategoryId, Name, CostCents, Type)
                         VALUES (@Id, @Date, @CategoryId, @Name, @CostCents, @Type)
                         ON CONFLICT(Id) DO UPDATE SET
                             Date = excluded.Date,
                             CategoryId = excluded.CategoryId,
                             Name = excluded.Name,
                             CostCents = excluded.CostCents,
                             Type = excluded.Type;
                     """, new
        {
            row.Id,
            Date = dateUnix,
            row.CategoryId,
            Name = row.Name.Trim(),
            CostCents = costCents,
            Type = (int)row.Type,
        });
    }


    public void Delete(string id)
    {
        using var conn = Database.OpenConnection();
        conn.Open();
        
        conn.Execute("DELETE FROM Transactions WHERE Id = @Id;", new { Id = id });
    }
    
    private static long LocalDateToUnixMidnight(DateTime date)
    {
        var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local);
        return new DateTimeOffset(localMidnight).ToUnixTimeSeconds();
    }

    private static DateTime UnixToLocalDate(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.Date;
    }

    private static long DollarsToCents(decimal dollars)
    {
        return (long)Math.Round(dollars * 100m, MidpointRounding.AwayFromZero);
    }

    private static decimal CentsToDollars(long cents) => cents / 100m;
}