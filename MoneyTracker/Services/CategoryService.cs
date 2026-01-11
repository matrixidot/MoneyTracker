using Dapper;
using MoneyTracker.Models;

namespace MoneyTracker.Services;

public sealed class CategoryService
{
    public IReadOnlyList<Category> GetAll()
    {
        using var conn = Database.OpenConnection();
        conn.Open();
        
        return conn.Query<Category>("SELECT Id, Name FROM Categories ORDER BY Name;").ToList();
    }

    public void EnsureSeeded()
    {
        using var conn = Database.OpenConnection();
        conn.Open();
        
        var count = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Categories;");
        if (count > 0) return;

        InsertCategory(conn, "00000000-0000-0000-0000-000000000001", "Food");
        InsertCategory(conn, "00000000-0000-0000-0000-000000000002", "Subscriptions");
        InsertCategory(conn, "00000000-0000-0000-0000-000000000003", "DeliveryFees");
        InsertCategory(conn, "00000000-0000-0000-0000-000000000004", "OnlinePurchases");
        InsertCategory(conn, "00000000-0000-0000-0000-000000000005", "Groceries");
        InsertCategory(conn, "00000000-0000-0000-0000-000000000006", "Misc");
    }

    private static void InsertCategory(Microsoft.Data.Sqlite.SqliteConnection conn, string id, string name)
    {
        conn.Execute("""
                         INSERT OR IGNORE INTO Categories (Id, Name)
                         VALUES (@Id, @Name);
                     """, new { Id = id, Name = name });
    }
}