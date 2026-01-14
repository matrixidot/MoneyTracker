using System.Collections.ObjectModel;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MoneyTracker.Models;
using MoneyTracker.MVVM;
using MoneyTracker.Services;

namespace MoneyTracker.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly CategoryService _categoryService = new();
    private readonly TransactionService _transactionService = new();

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<TransactionRow> Rows { get; } = new();

    private TransactionRow? _selectedRow;
    public TransactionRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                DeleteRowCommand.RaiseCanExecuteChanged();
            }
        }
    }


    private string _statusText = "Ready.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public DateTime SelectedMonth 
    {
        get => _selectedMonth;
        set 
        {
            var normalized = new DateTime(value.Year, value.Month, 1);
            if (SetProperty(ref _selectedMonth, normalized)) {
                Refresh();
            }
        }
    }

    public string SelectedMonthLabel => SelectedMonth.ToString("yyyy-MM");

    public ObservableCollection<MonthlyCategorySummary> MonthlySummary { get; } = new();

    private decimal _monthIncome;

    public decimal MonthIncome 
    {
        get => _monthIncome;
        set 
        {
            if (SetProperty(ref _monthIncome, value)) 
                RaisePropertyChanged(nameof(MonthNet));
        } 
    }

    private decimal _monthExpense;

    public decimal MonthExpense 
    {
        get => _monthExpense;
        set 
        {
            if (SetProperty(ref _monthExpense, value))
                RaisePropertyChanged(nameof(MonthNet));
        }
    }
    
    public decimal MonthNet => MonthIncome - MonthExpense;

    public ObservableCollection<ISeries> ExpensePieSeries { get; } = new();

    private Axis[] _trendXAxes = Array.Empty<Axis>();
    public Axis[] TrendXAxes
    {
        get => _trendXAxes;
        set => SetProperty(ref _trendXAxes, value);
    }

    public ObservableCollection<ISeries> TrendSeries { get; } = new();
    public ObservableCollection<string> TrendLabels { get; } = new();

    private string _newCategoryName = "";
    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public RelayCommand AddCategoryCommand { get; }

    
    public RelayCommand AddRowCommand { get; }
    public RelayCommand DeleteRowCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public MainViewModel()
    {
        AddRowCommand = new RelayCommand(AddRow);
        DeleteRowCommand = new RelayCommand(DeleteRow, () => SelectedRow != null);
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Refresh);
        AddCategoryCommand = new RelayCommand(AddCategory);

        Initialize();
    }

    private void Initialize()
    {
        LoadCategories();
        LoadRows();
        LoadMonthlySummary();
        StatusText = "Ready.";
    }

    private void LoadCategories()
    {
        Categories.Clear();
        foreach(var c in _categoryService.GetAll())
            Categories.Add(c);
    }

    private void LoadRows()
    {
        Rows.Clear();

        var catMap = Categories.ToDictionary(c => c.Id, c => c.Name);
        foreach (var r in _transactionService.GetByMonth(SelectedMonth))
        {
            r.CategoryName = catMap.TryGetValue(r.CategoryId, out var name) ? name : "";
            Rows.Add(r);
        }
    }

    private void AddRow()
    {
        var defaultCat = Categories.FirstOrDefault();
        var defaultDate = (SelectedMonth.Year == DateTime.Today.Year && SelectedMonth.Month == DateTime.Today.Month) ? DateTime.Today : SelectedMonth;
        
        var row = new TransactionRow
        {
            Date = defaultDate,
            Type = TransactionType.Expense,
            CategoryId = defaultCat?.Id ?? "",
            CategoryName = defaultCat?.Name ?? "",
            Name = "",
            Cost = 0m
        };
        
        Rows.Insert(0, row);
        SelectedRow = row;
        StatusText = "Row added (not saved yet).";
    }
    

    private void DeleteRow()
    {
        if (SelectedRow is null)
        {
            StatusText = "No row selected.";
            return;
        }

        _transactionService.Delete(SelectedRow.Id);
        Rows.Remove(SelectedRow);
        SelectedRow = null;
        StatusText = "Row deleted.";
    }

    private void Save()
    {
        try
        {
            var validCategoryIds = Categories.Select(c => c.Id).ToHashSet();

            var validRows = Rows.Where(r =>
                !string.IsNullOrWhiteSpace(r.Name) &&
                !string.IsNullOrWhiteSpace(r.CategoryId) &&
                validCategoryIds.Contains(r.CategoryId) &&
                r.Cost > 0m
            ).ToList();

            var invalidCount = Rows.Count - validRows.Count;

            foreach (var r in validRows)
                _transactionService.Upsert(r);

            StatusText = $"Saved {validRows.Count} rows. Skipped {invalidCount} invalid rows.";
            LoadMonthlySummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Save failed");
            throw;
        }
    }
    
    private void AddCategory()
    {
        try
        {
            var created = _categoryService.Create(NewCategoryName);

            Categories.Add(created);
            NewCategoryName = "";

            
            LoadMonthlySummary();
            BuildExpensePie();
            BuildTrends();

            StatusText = $"Added category: {created.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Add Category");
        }
    }


    private void LoadMonthlySummary() 
    {
        MonthlySummary.Clear();
        var summary = _transactionService.GetMonthlySummary(SelectedMonth, Categories);
        
        foreach (var s in summary)
            MonthlySummary.Add(s);
        
        MonthIncome = MonthlySummary.Sum(x => x.Income);
        MonthExpense = MonthlySummary.Sum(x => x.Expense);
        StatusText = $"Loaded {SelectedMonth:yyyy-MM} summary.";

        BuildExpensePie();
        BuildTrends();
    }

    private void BuildExpensePie()
    {
        ExpensePieSeries.Clear();
        
        var slices = MonthlySummary
            .Where(x => x.Expense > 0m)
            .OrderByDescending(x => x.Expense)
            .ToList();


        foreach (var s in slices)
        {
            ExpensePieSeries.Add(new PieSeries<decimal> {
                Values = new[] {s.Expense},
                Name = s.CategoryName,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = p => $"{s.CategoryName}\n{p.Coordinate.PrimaryValue:C}"
            });
        }
    }
    
    private void BuildTrends()
    {
        TrendSeries.Clear();

        var data = _transactionService.GetMonthlyTotalsSeries(SelectedMonth, 12);

        var labels = data.Select(p => p.Ym).ToArray();

        TrendSeries.Add(new LineSeries<decimal>
        {
            Name = "Income",
            Values = data.Select(x => x.Income).ToArray()
        });

        TrendSeries.Add(new LineSeries<decimal>
        {
            Name = "Expense",
            Values = data.Select(x => x.Expense).ToArray()
        });

        TrendSeries.Add(new LineSeries<decimal>
        {
            Name = "Net",
            Values = data.Select(x => x.Income - x.Expense).ToArray()
        });

        TrendXAxes = new[]
        {
            new Axis
            {
                Labels = labels
            }
        };
    }



    private void Refresh()
    {
        LoadCategories();
        LoadRows();
        LoadMonthlySummary();
        StatusText = "Refreshed.";
    }
}