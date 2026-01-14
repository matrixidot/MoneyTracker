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

    public IReadOnlyList<TransactionRow> GetByMonth(DateTime month) 
    {
        using var conn = Database.OpenConnection();
        conn.Open();


        var startLocal = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var endLocal = startLocal.AddMonths(1);
        
        var startUnix = new DateTimeOffset(startLocal).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(endLocal).ToUnixTimeSeconds();

        var rows = conn.Query("""
                              SELECT Id, Date, CategoryId, Name, CostCents, Type
                              FROM Transactions
                              WHERE Date >= @Start and DATE < @End
                              ORDER BY Date DESC, Name ASC;
                              """, new { Start = startUnix, End = endUnix }).ToList();
        return rows.Select(r => new TransactionRow {
            Id = (string)r.Id,
            Date = UnixToLocalDate((long)r.Date),
            CategoryId = (string)r.CategoryId,
            Name = (string)r.Name,
            Cost = CentsToDollars((long)r.CostCents),
            Type = (TransactionType)(long)r.Type,
        }).ToList();
    }

    public IReadOnlyList<MonthlyCategorySummary> GetMonthlySummary(DateTime month, IReadOnlyList<Category> categories) 
    {
        using var conn = Database.OpenConnection();
        conn.Open();
        
        var startLocal = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var endLocal = startLocal.AddMonths(1);
        
        var startUnix = new DateTimeOffset(startLocal).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(endLocal).ToUnixTimeSeconds();
        
        var rows = conn.Query("""
                              SELECT CategoryId, Type, SUM(CostCents) AS TotalCents
                              FROM Transactions
                              WHERE Date >= @Start and Date < @End
                              GROUP BY CategoryId, Type;
                              """, new { Start = startUnix, End = endUnix }).ToList();

        var catMap = categories.ToDictionary(c => c.Id, c => c.Name);

        var byCategory = rows.GroupBy(r => (string)r.CategoryId);
        
        var result = new List<MonthlyCategorySummary>();

        foreach (var g in byCategory) 
        {
            var catId = g.Key;
            var name = catMap.TryGetValue(catId, out var category) ? category : "(Unknown)";

            long incomeCents = 0;
            long expenseCents = 0;

            foreach (var r in g) 
            {
                var type = (long)r.Type;
                var total = (long)r.TotalCents;

                if (type is (long)TransactionType.Income or (long)TransactionType.Refund) incomeCents = total;
                else expenseCents = total;
            }
            
            result.Add(new MonthlyCategorySummary 
            {
                CategoryId = catId,
                CategoryName = name,
                Income = incomeCents / 100m,
                Expense = expenseCents / 100m,
            });
        }

        foreach (var c in categories)
        {
            if (result.All(x => x.CategoryId != c.Id))
                result.Add(new MonthlyCategorySummary { CategoryId = c.Id, CategoryName = c.Name, Income = 0m, Expense = 0m });
        }

        return result.OrderByDescending(x => x.Expense).ThenBy(x => x.CategoryName).ToList();
    }
    
    public IReadOnlyList<(string Ym, decimal Income, decimal Expense)> GetMonthlyTotalsSeries(DateTime endMonthInclusive, int monthsBack)
    {
        using var conn = Database.OpenConnection();
        conn.Open();

        var endStart = new DateTime(endMonthInclusive.Year, endMonthInclusive.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var start = endStart.AddMonths(-(monthsBack - 1));
        var endExclusive = endStart.AddMonths(1);

        var startUnix = new DateTimeOffset(start).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(endExclusive).ToUnixTimeSeconds();

        var rows = conn.Query("""
                                  SELECT
                                    strftime('%Y-%m', datetime(Date, 'unixepoch')) AS Ym,
                                    Type,
                                    SUM(CostCents) AS TotalCents
                                  FROM Transactions
                                  WHERE Date >= @Start AND Date < @End
                                  GROUP BY Ym, Type
                                  ORDER BY Ym;
                              """, new { Start = startUnix, End = endUnix }).ToList();

        var map = new Dictionary<string, (long income, long expense)>();

        foreach (var r in rows)
        {
            var ym = (string)r.Ym;
            var type = (long)r.Type;
            var cents = (long)r.TotalCents;

            map.TryGetValue(ym, out var cur);

            if (type == (long)TransactionType.Income) cur.income = cents;
            else cur.expense = cents;

            map[ym] = cur;
        }

        var result = new List<(string Ym, decimal Income, decimal Expense)>();
        for (var dt = start; dt < endExclusive; dt = dt.AddMonths(1))
        {
            var ym = dt.ToString("yyyy-MM");
            map.TryGetValue(ym, out var cur);

            result.Add((ym, cur.income / 100m, cur.expense / 100m));
        }

        return result;
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