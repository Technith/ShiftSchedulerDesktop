using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Models;

namespace ShiftSchedulerDesktop.ViewModels;

public class StoresViewModel : ViewModelBase
{
    Store? _sel;
    bool _addVis, _editVis;
    string _name = "";
    TimeSpan? _newOpen, _newClose, _editOpen, _editClose;
    string? _selNewOpen, _selNewClose, _selEditOpen, _selEditClose;

    public ObservableCollection<Store> Stores { get; } = new();
    public ObservableCollection<EmployeeSelection> AllEmployees { get; } = new();
    public ObservableCollection<string> TimeOptions { get; } = new();

    public Store? SelectedStore
    {
        get => _sel;
        set { if (SetProperty(ref _sel, value)) { (EditCommand as RelayCommand)?.RaiseCanExecuteChanged(); (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
    }

    public bool IsAddFormVisible { get => _addVis; set => SetProperty(ref _addVis, value); }
    public bool IsEditFormVisible { get => _editVis; set => SetProperty(ref _editVis, value); }
    public string NewStoreName { get => _name; set => SetProperty(ref _name, value); }
    public TimeSpan? NewOpenTime { get => _newOpen; set => SetProperty(ref _newOpen, value); }
    public TimeSpan? NewCloseTime { get => _newClose; set => SetProperty(ref _newClose, value); }
    public TimeSpan? EditOpenTime { get => _editOpen; set => SetProperty(ref _editOpen, value); }
    public TimeSpan? EditCloseTime { get => _editClose; set => SetProperty(ref _editClose, value); }

    public string? SelectedNewOpenTime { get => _selNewOpen; set { if (SetProperty(ref _selNewOpen, value)) NewOpenTime = ParseTime(value); } }
    public string? SelectedNewCloseTime { get => _selNewClose; set { if (SetProperty(ref _selNewClose, value)) NewCloseTime = ParseTime(value); } }
    public string? SelectedEditOpenTime { get => _selEditOpen; set { if (SetProperty(ref _selEditOpen, value)) EditOpenTime = ParseTime(value); } }
    public string? SelectedEditCloseTime { get => _selEditClose; set { if (SetProperty(ref _selEditClose, value)) EditCloseTime = ParseTime(value); } }

    public ICommand AddCommand { get; }
    public ICommand CommitAddCommand { get; }
    public ICommand CancelAddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CommitEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteCommand { get; }

    public StoresViewModel()
    {
        AddCommand = new RelayCommand(ShowAdd);
        CommitAddCommand = new RelayCommand(DoAdd);
        CancelAddCommand = new RelayCommand(() => IsAddFormVisible = false);
        EditCommand = new RelayCommand(ShowEdit, () => _sel != null);
        CommitEditCommand = new RelayCommand(DoEdit);
        CancelEditCommand = new RelayCommand(() => { IsEditFormVisible = false; LoadStores(); });
        DeleteCommand = new RelayCommand(DoDelete, () => _sel != null);

        for (int h = 0; h < 24; h++)
            for (int m = 0; m < 60; m += 15)
                TimeOptions.Add(FmtTime(new TimeSpan(h, m, 0)));

        LoadStores();
        LoadEmployees();
    }

    string FmtTime(TimeSpan t)
    {
        int h = t.Hours % 12;
        return $"{(h == 0 ? 12 : h)}:{t.Minutes:D2} {(t.Hours >= 12 ? "PM" : "AM")}";
    }

    TimeSpan? ParseTime(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var p = s.Split(' ');
        if (p.Length != 2) return null;
        var tp = p[0].Split(':');
        if (tp.Length != 2 || !int.TryParse(tp[0], out int h) || !int.TryParse(tp[1], out int m)) return null;
        bool pm = p[1].Equals("PM", StringComparison.OrdinalIgnoreCase);
        if (h == 12) h = pm ? 12 : 0;
        else if (pm) h += 12;
        return new TimeSpan(h, m, 0);
    }

    void LoadStores()
    {
        Stores.Clear();
        using var ctx = new DatabaseContext();
        foreach (var s in ctx.StoreTable?.Include(x => x.Employees).ToList() ?? [])
            Stores.Add(s);
    }

    void LoadEmployees()
    {
        AllEmployees.Clear();
        using var ctx = new DatabaseContext();
        foreach (var e in ctx.EmployeeTable?.ToList() ?? [])
            AllEmployees.Add(new EmployeeSelection { Employee = e });
    }

    void SyncEmployeeSelections()
    {
        if (_sel == null) return;
        foreach (var es in AllEmployees)
            es.IsSelected = _sel.Employees?.Any(e => e.Id == es.Employee.Id) ?? false;
    }

    void ShowAdd()
    {
        LoadEmployees();
        NewStoreName = "";
        NewOpenTime = new TimeSpan(9, 0, 0);
        NewCloseTime = new TimeSpan(17, 0, 0);
        SelectedNewOpenTime = FmtTime(NewOpenTime.Value);
        SelectedNewCloseTime = FmtTime(NewCloseTime.Value);
        foreach (var e in AllEmployees) e.IsSelected = false;
        IsAddFormVisible = true;
    }

    void DoAdd()
    {
        if (string.IsNullOrWhiteSpace(NewStoreName)) return;
        using var ctx = new DatabaseContext();

        var store = new Store
        {
            Name = NewStoreName,
            OpenTime = DateTime.Today + NewOpenTime,
            CloseTime = DateTime.Today + NewCloseTime,
            Employees = new()
        };
        ctx.StoreTable?.Add(store);

        foreach (var id in AllEmployees.Where(e => e.IsSelected).Select(e => e.Employee.Id))
        {
            var emp = ctx.EmployeeTable?.Find(id);
            if (emp != null) store.Employees.Add(emp);
        }

        ctx.SaveChanges();
        IsAddFormVisible = false;
        LoadStores();
    }

    void ShowEdit()
    {
        LoadEmployees();
        SyncEmployeeSelections();
        EditOpenTime = _sel?.OpenTime?.TimeOfDay;
        EditCloseTime = _sel?.CloseTime?.TimeOfDay;
        SelectedEditOpenTime = EditOpenTime.HasValue ? FmtTime(EditOpenTime.Value) : null;
        SelectedEditCloseTime = EditCloseTime.HasValue ? FmtTime(EditCloseTime.Value) : null;
        IsEditFormVisible = true;
    }

    void DoEdit()
    {
        if (_sel == null) return;
        using var ctx = new DatabaseContext();

        var store = ctx.StoreTable?.Include(x => x.Employees).Include(x => x.Schedule).FirstOrDefault(x => x.Id == _sel.Id);
        if (store == null) return;

        store.Name = _sel.Name;
        store.OpenTime = EditOpenTime.HasValue ? DateTime.Today + EditOpenTime.Value : null;
        store.CloseTime = EditCloseTime.HasValue ? DateTime.Today + EditCloseTime.Value : null;

        // Adjust existing shifts to fit within new store hours
        if (store.Schedule != null && (EditOpenTime.HasValue || EditCloseTime.HasValue))
        {
            var shiftsToRemove = new List<Shift>();
            foreach (var shift in store.Schedule)
            {
                var shiftDate = shift.StartTime.Date;
                var shiftStart = shift.StartTime.TimeOfDay;
                var shiftEnd = shift.EndTime.TimeOfDay;

                // Clamp start time to new open time
                if (EditOpenTime.HasValue && shiftStart < EditOpenTime.Value)
                    shiftStart = EditOpenTime.Value;

                // Clamp end time to new close time
                if (EditCloseTime.HasValue && shiftEnd > EditCloseTime.Value)
                    shiftEnd = EditCloseTime.Value;

                // If shift becomes invalid (start >= end), mark for removal
                if (shiftStart >= shiftEnd)
                {
                    shiftsToRemove.Add(shift);
                }
                else
                {
                    shift.StartTime = shiftDate + shiftStart;
                    shift.EndTime = shiftDate + shiftEnd;
                }
            }

            // Remove invalid shifts
            foreach (var shift in shiftsToRemove)
            {
                store.Schedule.Remove(shift);
                ctx.ShiftTable?.Remove(shift);
            }
        }

        var selIds = AllEmployees.Where(e => e.IsSelected).Select(e => e.Employee.Id).ToHashSet();
        store.Employees ??= new();

        foreach (var e in store.Employees.Where(e => !selIds.Contains(e.Id)).ToList())
            store.Employees.Remove(e);

        var curIds = store.Employees.Select(e => e.Id).ToHashSet();
        foreach (var id in selIds.Where(id => !curIds.Contains(id)))
        {
            var emp = ctx.EmployeeTable?.Find(id);
            if (emp != null) store.Employees.Add(emp);
        }

        ctx.SaveChanges();
        IsEditFormVisible = false;
        LoadStores();
    }

    void DoDelete()
    {
        if (_sel == null) return;
        using var ctx = new DatabaseContext();
        var store = ctx.StoreTable?.Find(_sel.Id);
        if (store != null) { ctx.StoreTable?.Remove(store); ctx.SaveChanges(); }
        LoadStores();
    }
}

public class EmployeeSelection : ViewModelBase
{
    bool _sel;
    public Employee Employee { get; set; } = new();
    public bool IsSelected { get => _sel; set => SetProperty(ref _sel, value); }
}
