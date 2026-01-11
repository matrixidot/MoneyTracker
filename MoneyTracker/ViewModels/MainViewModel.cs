using System.Collections.ObjectModel;
using System.Windows;
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

        Initialize();
    }

    private void Initialize()
    {
        _categoryService.EnsureSeeded();
        LoadCategories();
        LoadRows();
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
        foreach (var r in _transactionService.GetAll())
        {
            r.CategoryName = catMap.TryGetValue(r.CategoryId, out var name) ? name : "";
            Rows.Add(r);
        }
    }

    private void AddRow()
    {
        var defaultCat = Categories.FirstOrDefault();
        var row = new TransactionRow
        {
            Date = DateTime.Today,
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
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Save failed");
            throw;
        }
    }

    private void Refresh()
    {
        LoadCategories();
        LoadRows();
        StatusText = "Refreshed.";
    }
}