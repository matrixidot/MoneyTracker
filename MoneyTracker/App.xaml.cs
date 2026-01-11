using System.Configuration;
using System.Data;
using System.Windows;
using Dapper;

namespace MoneyTracker;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    override protected void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        Database.Initialize();
    }
}