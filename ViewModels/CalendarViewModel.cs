using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Models;
using ShiftSchedulerDesktop.Models;

namespace ShiftSchedulerDesktop.ViewModels;

public class CalendarViewModel : ViewModelBase
{
    private readonly StoresViewModel _storesVM;
    private Store? _selectedStore;
    private string _saveStatusMessage = string.Empty;

    public Store? SelectedStore
    {
        get => _selectedStore;
        set
        {
            if (SetProperty(ref _selectedStore, value))
            {
                GenerateSchedule();
                LoadStoreEmployees();
            }
        }
    }

    public string SaveStatusMessage
    {
        get => _saveStatusMessage;
        set => SetProperty(ref _saveStatusMessage, value);
    }

    public ObservableCollection<Store> Stores => _storesVM.Stores;
    public ObservableCollection<Employee> StoreEmployees { get; } = new();
    public ObservableCollection<string> HourSlots { get; } = new();
    public ObservableCollection<ScheduleDay> Days { get; } = new();

    private ScheduleShift? _draggedShift;
    private ScheduleDay? _dragSource;
    private Employee? _draggedEmployee;

    public ScheduleShift? DraggedShift
    {
        get => _draggedShift;
        set => SetProperty(ref _draggedShift, value);
    }

    public ScheduleDay? DragSource
    {
        get => _dragSource;
        set => SetProperty(ref _dragSource, value);
    }

    public Employee? DraggedEmployee
    {
        get => _draggedEmployee;
        set => SetProperty(ref _draggedEmployee, value);
    }

    public RelayCommand SaveScheduleCommand { get; }

    public CalendarViewModel(StoresViewModel storesVM)
    {
        _storesVM = storesVM;

        SaveScheduleCommand = new RelayCommand(SaveSchedule, () => SelectedStore != null);
        _storesVM.PropertyChanged += OnStoresVMPropertyChanged;
        _storesVM.Stores.CollectionChanged += OnStoresCollectionChanged;

        if (_storesVM.Stores.Count > 0)
        {
            SelectedStore = _storesVM.Stores[0];
        }

        GenerateSchedule();
    }

    private void OnStoresVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StoresViewModel.SelectedStore))
        {
            SelectedStore = _storesVM.SelectedStore;
        }
    }

    private void OnStoresCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedStore == null && _storesVM.Stores.Count > 0)
            SelectedStore = _storesVM.Stores[0];
    }

    private void GenerateSchedule()
    {
        HourSlots.Clear();
        Days.Clear();

        int open = SelectedStore?.OpenTime?.Hour ?? 9;
        int close = SelectedStore?.CloseTime?.Hour ?? 17;

        for (int h = open; h <= close; h++)
            HourSlots.Add(FormatHour(h));

        var shifts = LoadShiftsFromDb();

        foreach (var name in new[] { "Mon", "Tue", "Wed", "Thu", "Fri" })
        {
            var day = new ScheduleDay { DayName = name };
            foreach (var s in shifts.Where(x => GetDayName(x.StartTime.DayOfWeek) == name))
            {
                day.Shifts.Add(new ScheduleShift
                {
                    EmployeeName = s.AssignedEmployee?.Name ?? "Unassigned",
                    EmployeeId = s.AssignedEmployee?.Id ?? 0,
                    StartHour = s.StartTime.Hour,
                    EndHour = s.EndTime.Hour,
                    Color = GetEmployeeColor(s.AssignedEmployee)
                });
            }
            Days.Add(day);
        }
    }

    private List<Shift> LoadShiftsFromDb()
    {
        if (SelectedStore == null) return new();

        using var ctx = new DatabaseContext();
        var store = ctx.StoreTable?
            .Include(s => s.Schedule).ThenInclude(sh => sh.AssignedEmployee)
            .FirstOrDefault(s => s.Id == SelectedStore.Id);
        return store?.Schedule?.ToList() ?? new();
    }

    private void LoadStoreEmployees()
    {
        StoreEmployees.Clear();
        if (SelectedStore == null) return;

        using var ctx = new DatabaseContext();
        var store = ctx.StoreTable?.Include(s => s.Employees)
            .FirstOrDefault(s => s.Id == SelectedStore.Id);

        if (store?.Employees != null)
            foreach (var e in store.Employees)
                StoreEmployees.Add(e);
    }

    private static readonly string[] _shiftColors = { "#4A90D9", "#E67E22", "#27AE60", "#9B59B6", "#E74C3C", "#1ABC9C", "#F39C12" };

    private string FormatHour(int h) => h switch
    {
        0 => "12 AM",
        12 => "12 PM",
        < 12 => $"{h} AM",
        _ => $"{h - 12} PM"
    };

    private string GetDayName(DayOfWeek d) => d.ToString()[..3];

    private string GetEmployeeColor(Employee? emp) =>
        emp == null ? "#808080" : _shiftColors[emp.Id % _shiftColors.Length];

    public void StartEmployeeDrag(Employee emp)
    {
        DraggedEmployee = emp;
        DraggedShift = null;
        DragSource = null;
    }

    public void StartShiftDrag(ScheduleDay src, ScheduleShift shift)
    {
        DragSource = src;
        DraggedShift = shift;
        DraggedEmployee = null;
    }

    public void DropOnSchedule(ScheduleDay target, int hour)
    {
        if (DraggedEmployee != null)
        {
            int close = SelectedStore?.CloseTime?.Hour ?? 17;
            target.Shifts.Add(new ScheduleShift
            {
                EmployeeName = DraggedEmployee.Name,
                EmployeeId = DraggedEmployee.Id,
                StartHour = hour,
                EndHour = Math.Min(hour + 4, close),
                Color = GetEmployeeColor(DraggedEmployee)
            });
        }
        else if (DraggedShift != null && DragSource != null && DragSource != target)
        {
            DragSource.Shifts.Remove(DraggedShift);
            int len = DraggedShift.EndHour - DraggedShift.StartHour;
            DraggedShift.StartHour = hour;
            DraggedShift.EndHour = hour + len;
            target.Shifts.Add(DraggedShift);
        }
        CancelDrag();
    }

    public void CancelDrag()
    {
        DraggedEmployee = null;
        DraggedShift = null;
        DragSource = null;
        foreach (var d in Days) d.IsDragOver = false;
    }

    public void SetDragOver(ScheduleDay? day)
    {
        foreach (var d in Days) d.IsDragOver = d == day;
    }

    public void ResizeShift(ScheduleShift shift, int start, int end)
    {
        int open = SelectedStore?.OpenTime?.Hour ?? 9;
        int close = SelectedStore?.CloseTime?.Hour ?? 17;
        shift.StartHour = Math.Clamp(start, open, close - 1);
        shift.EndHour = Math.Clamp(end, shift.StartHour + 1, close);
    }

    public void DeleteShift(ScheduleDay day, ScheduleShift shift) => day.Shifts.Remove(shift);

    private void SaveSchedule()
    {
        if (SelectedStore == null) return;
        SaveStatusMessage = string.Empty;

        using var ctx = new DatabaseContext();
        var store = ctx.StoreTable?
            .Include(s => s.Schedule).Include(s => s.Employees)
            .FirstOrDefault(s => s.Id == SelectedStore.Id);

        if (store == null) return;

        if (store.Schedule != null)
        {
            ctx.ShiftTable?.RemoveRange(store.Schedule);
            store.Schedule.Clear();
        }
        else store.Schedule = new List<Shift>();

        var monday = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7);

        for (int i = 0; i < Days.Count; i++)
        {
            var date = monday.AddDays(i);
            foreach (var ss in Days[i].Shifts)
            {
                store.Schedule.Add(new Shift
                {
                    StartTime = date.AddHours(ss.StartHour),
                    EndTime = date.AddHours(ss.EndHour),
                    AssignedEmployee = ctx.EmployeeTable?.Find(ss.EmployeeId)
                });
            }
        }

        ctx.SaveChanges();
        SaveStatusMessage = $"Saved {DateTime.Now:h:mm tt}";
    }
}

public class ScheduleDay : INotifyPropertyChanged
{
    private string _name = "";
    private bool _dragOver;

    public string DayName { get => _name; set { _name = value; Notify(); } }
    public bool IsDragOver { get => _dragOver; set { _dragOver = value; Notify(); } }
    public ObservableCollection<ScheduleShift> Shifts { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([System.Runtime.CompilerServices.CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ScheduleShift : INotifyPropertyChanged
{
    private string _empName = "";
    private int _empId, _start, _end;
    private string _color = "#4A90D9";

    public string EmployeeName { get => _empName; set { _empName = value; Notify(); } }
    public int EmployeeId { get => _empId; set { _empId = value; Notify(); } }
    public int StartHour { get => _start; set { _start = value; Notify(); Notify(nameof(TimeDisplay)); } }
    public int EndHour { get => _end; set { _end = value; Notify(); Notify(nameof(TimeDisplay)); } }
    public string Color { get => _color; set { _color = value; Notify(); } }

    public string TimeDisplay => $"{Fmt(StartHour)} - {Fmt(EndHour)}";
    public int Duration => EndHour - StartHour;

    static string Fmt(int h) => h switch { 0 => "12 AM", 12 => "12 PM", < 12 => $"{h} AM", _ => $"{h - 12} PM" };

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([System.Runtime.CompilerServices.CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
