using MoneyTracker.MVVM;

namespace MoneyTracker.Models;

public sealed class TransactionRow : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private DateTime _date = DateTime.Today;
    public DateTime Date
    {
        get => _date;
        set { if (SetProperty(ref _date, value)) RaisePropertyChanged(nameof(IsValid)); }
    }

    private TransactionType _type = TransactionType.Expense;
    public TransactionType Type
    {
        get => _type;
        set { if (SetProperty(ref _type, value)) RaisePropertyChanged(nameof(IsValid)); }
    }

    private string _categoryId = "";
    public string CategoryId
    {
        get => _categoryId;
        set { if (SetProperty(ref _categoryId, value)) RaisePropertyChanged(nameof(IsValid)); }
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) RaisePropertyChanged(nameof(IsValid)); }
    }

    private decimal _cost;
    public decimal Cost
    {
        get => _cost;
        set { if (SetProperty(ref _cost, value)) RaisePropertyChanged(nameof(IsValid)); }
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(CategoryId) &&
        Cost > 0m;

    private string _categoryName = "";
    public string CategoryName
    {
        get => _categoryName;
        set { if (SetProperty(ref _categoryName, value)) RaisePropertyChanged(nameof(IsValid));}
    }
}