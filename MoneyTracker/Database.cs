using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MoneyTracker;

public static class Database
{
    private static readonly string DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MoneyTracker", "transactions.db");

    public static SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        return new SqliteConnection($"Data Source={DbPath};Cache=Shared");
    }

    public static void Initialize()
    {
        using var conn = OpenConnection();
        conn.Open();

        conn.Execute("PRAGMA foreign_keys = ON;");
        conn.Execute("PRAGMA journal_mode = WAL;");

        conn.Execute("""
                         CREATE TABLE IF NOT EXISTS Categories (
                             Id TEXT PRIMARY KEY,
                             Name TEXT NOT NULL UNIQUE
                         );

                         CREATE TABLE IF NOT EXISTS Transactions (
                             Id TEXT PRIMARY KEY,
                             Date INTEGER NOT NULL,
                             CategoryId TEXT NOT NULL,
                             Name TEXT NOT NULL,
                             CostCents INTEGER NOT NULL,
                             Type INTEGER NOT NULL DEFAULT 0,
                             FOREIGN KEY(CategoryId) REFERENCES Categories(Id)
                         );
                     
                         CREATE TABLE IF NOT EXISTS DailyCheckins (
                             Day TEXT PRIMARY KEY,
                             Reviewed INTEGER NOT NULL CHECK (Reviewed IN (0, 1))
                         );

                         CREATE INDEX IF NOT EXISTS IX_Transactions_Date
                             ON Transactions(Date);

                         CREATE INDEX IF NOT EXISTS IX_Transactions_Category
                             ON Transactions(CategoryId);
                     """);
        
        var hasType = conn.ExecuteScalar<long>("""
                                                   SELECT COUNT(*)
                                                   FROM pragma_table_info('Transactions')
                                                   WHERE name = 'Type';
                                               """);

        if (hasType == 0)
        {
            conn.Execute("ALTER TABLE Transactions ADD COLUMN Type INTEGER NOT NULL DEFAULT 0;");
        }
    }


    public static string GetDbPath() => DbPath;
}