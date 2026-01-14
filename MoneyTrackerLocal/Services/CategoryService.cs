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

    public Category Create(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new ArgumentException("Category name cannot be empty.");

        using var conn = Database.OpenConnection();
        conn.Open();

        // Prevent duplicates (case-insensitive)
        var exists = conn.ExecuteScalar<long>("""
                                                  SELECT COUNT(*) FROM Categories
                                                  WHERE lower(Name) = lower(@Name);
                                              """, new { Name = name });

        if (exists > 0)
            throw new InvalidOperationException("That category already exists.");

        var cat = new Category
        {
            Id = Guid.NewGuid().ToString(),
            Name = name
        };

        conn.Execute("""
                         INSERT INTO Categories (Id, Name)
                         VALUES (@Id, @Name);
                     """, cat);

        return cat;
    }
    
    private static void InsertCategory(Microsoft.Data.Sqlite.SqliteConnection conn, string id, string name)
    {
        conn.Execute("""
                         INSERT OR IGNORE INTO Categories (Id, Name)
                         VALUES (@Id, @Name);
                     """, new { Id = id, Name = name });
    }
}