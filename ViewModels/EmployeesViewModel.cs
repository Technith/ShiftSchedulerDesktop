using System.Collections.ObjectModel;
using System.Windows.Input;
using Models;

namespace ShiftSchedulerDesktop.ViewModels;

public class EmployeesViewModel : ViewModelBase
{
    Employee? _sel;
    bool _addVis, _editVis;
    string _name = "", _phone = "";

    public ObservableCollection<Employee> Employees { get; } = new();

    public Employee? SelectedEmployee
    {
        get => _sel;
        set { if (SetProperty(ref _sel, value)) { (EditCommand as RelayCommand)?.RaiseCanExecuteChanged(); (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
    }

    public bool IsAddFormVisible { get => _addVis; set => SetProperty(ref _addVis, value); }
    public bool IsEditFormVisible { get => _editVis; set => SetProperty(ref _editVis, value); }
    public string NewEmployeeName { get => _name; set => SetProperty(ref _name, value); }
    public string NewEmployeePhone { get => _phone; set => SetProperty(ref _phone, value); }

    public ICommand AddCommand { get; }
    public ICommand CommitAddCommand { get; }
    public ICommand CancelAddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteCommand { get; }

    public EmployeesViewModel()
    {
        AddCommand = new RelayCommand(() => { NewEmployeeName = ""; NewEmployeePhone = ""; IsAddFormVisible = true; });
        CommitAddCommand = new RelayCommand(Add);
        CancelAddCommand = new RelayCommand(() => IsAddFormVisible = false);
        EditCommand = new RelayCommand(() => IsEditFormVisible = true, () => _sel != null);
        CommitEditCommand = new RelayCommand(Edit);
        CancelEditCommand = new RelayCommand(() => { IsEditFormVisible = false; Load(); });
        DeleteCommand = new RelayCommand(Delete, () => _sel != null);
        Load();
    }

    void Load()
    {
        Employees.Clear();
        using var ctx = new DatabaseContext();
        foreach (var e in ctx.EmployeeTable?.ToList() ?? [])
            Employees.Add(e);
    }

    void Add()
    {
        if (string.IsNullOrWhiteSpace(NewEmployeeName)) return;
        using var ctx = new DatabaseContext();
        ctx.EmployeeTable?.Add(new Employee { Name = NewEmployeeName, Phone = NewEmployeePhone });
        ctx.SaveChanges();
        IsAddFormVisible = false;
        Load();
    }

    void Edit()
    {
        if (_sel == null) return;
        using var ctx = new DatabaseContext();
        var emp = ctx.EmployeeTable?.Find(_sel.Id);
        if (emp != null) { emp.Name = _sel.Name; emp.Phone = _sel.Phone; ctx.SaveChanges(); }
        IsEditFormVisible = false;
        Load();
    }

    void Delete()
    {
        if (_sel == null) return;
        using var ctx = new DatabaseContext();
        var emp = ctx.EmployeeTable?.Find(_sel.Id);
        if (emp != null) { ctx.EmployeeTable?.Remove(emp); ctx.SaveChanges(); }
        Load();
    }
}
