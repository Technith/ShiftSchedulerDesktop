using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Models;
using ShiftSchedulerDesktop.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure; // Test
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
                LoadAvailability();
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
    public ObservableCollection<ScheduleDay> AvailabilityDays { get; } = new();

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
    public RelayCommand ExportPdfCommand { get; }

    public CalendarViewModel(StoresViewModel storesVM)
    {
        _storesVM = storesVM;

        SaveScheduleCommand = new RelayCommand(SaveSchedule, () => SelectedStore != null);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => SelectedStore != null);
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

        double open = SelectedStore?.OpenTime?.TimeOfDay.TotalHours ?? 9;
        double close = SelectedStore?.CloseTime?.TimeOfDay.TotalHours ?? 17;

        for (double h = open; h <= close; h += 0.5)
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
                    StartHour = s.StartTime.Hour + s.StartTime.Minute / 60.0,
                    EndHour = s.EndTime.Hour + s.EndTime.Minute / 60.0,
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

    private void LoadAvailability()
    {
        AvailabilityDays.Clear();
        if (SelectedStore == null) return;

        using var ctx = new DatabaseContext();
        var availabilities = ctx.AvailabilityTable?
            .Where(a => a.StoreId == SelectedStore.Id)
            .Include(a => a.Employee)
            .ToList() ?? new();

        var dayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" };
        var dayMap = new Dictionary<string, DayOfWeek>
        {
            { "Mon", DayOfWeek.Monday },
            { "Tue", DayOfWeek.Tuesday },
            { "Wed", DayOfWeek.Wednesday },
            { "Thu", DayOfWeek.Thursday },
            { "Fri", DayOfWeek.Friday }
        };

        foreach (var dayName in dayNames)
        {
            var day = new ScheduleDay { DayName = dayName };
            var dow = dayMap[dayName];

            foreach (var avail in availabilities.Where(a => a.DayOfWeek == dow && a.StartTime.HasValue && a.EndTime.HasValue))
            {
                day.Shifts.Add(new ScheduleShift
                {
                    EmployeeName = avail.Employee?.Name ?? "Unknown",
                    EmployeeId = avail.Employee?.Id ?? 0,
                    StartHour = avail.StartTime!.Value.TotalHours,
                    EndHour = avail.EndTime!.Value.TotalHours,
                    Color = GetEmployeeColor(avail.Employee)
                });
            }

            AvailabilityDays.Add(day);
        }
    }

    private static readonly string[] _shiftColors = { "#4A90D9", "#E67E22", "#27AE60", "#9B59B6", "#E74C3C", "#1ABC9C", "#F39C12" };

    private string FormatHour(double h)
    {
        int hour = (int)h;
        int min = (int)((h - hour) * 60);
        string suffix = hour >= 12 ? "PM" : "AM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        return min == 0 ? $"{displayHour} {suffix}" : $"{displayHour}:{min:D2} {suffix}";
    }

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

    public void DropOnSchedule(ScheduleDay target, double hour)
    {
        if (DraggedEmployee != null)
        {
            double close = SelectedStore?.CloseTime?.TimeOfDay.TotalHours ?? 17;
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
            double len = DraggedShift.EndHour - DraggedShift.StartHour;
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

    public void ResizeShift(ScheduleShift shift, double start, double end)
    {
        double open = SelectedStore?.OpenTime?.TimeOfDay.TotalHours ?? 9;
        double close = SelectedStore?.CloseTime?.TimeOfDay.TotalHours ?? 17;
        shift.StartHour = Math.Clamp(start, open, close - 0.5);
        shift.EndHour = Math.Clamp(end, shift.StartHour + 0.5, close);
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
                    StartTime = date.Add(TimeSpan.FromHours(ss.StartHour)),
                    EndTime = date.Add(TimeSpan.FromHours(ss.EndHour)),
                    AssignedEmployee = ctx.EmployeeTable?.Find(ss.EmployeeId)
                });
            }
        }

        // Save availability
        var existingAvailability = ctx.AvailabilityTable?
            .Where(a => a.StoreId == SelectedStore.Id)
            .ToList();
        if (existingAvailability != null)
            ctx.AvailabilityTable?.RemoveRange(existingAvailability);

        var availDayMap = new Dictionary<string, DayOfWeek>
        {
            { "Mon", DayOfWeek.Monday },
            { "Tue", DayOfWeek.Tuesday },
            { "Wed", DayOfWeek.Wednesday },
            { "Thu", DayOfWeek.Thursday },
            { "Fri", DayOfWeek.Friday }
        };

        foreach (var day in AvailabilityDays)
        {
            var dow = availDayMap[day.DayName];
            foreach (var shift in day.Shifts)
            {
                ctx.AvailabilityTable?.Add(new EmployeeAvailability
                {
                    EmployeeId = shift.EmployeeId,
                    StoreId = SelectedStore.Id,
                    DayOfWeek = dow,
                    StartTime = TimeSpan.FromHours(shift.StartHour),
                    EndTime = TimeSpan.FromHours(shift.EndHour)
                });
            }
        }

        ctx.SaveChanges();
        SaveStatusMessage = $"Saved {DateTime.Now:h:mm tt}";
    }

    private void ExportPdf()
    {
        if (SelectedStore == null) return;

        QuestPDF.Settings.License = LicenseType.Community;

        var storeName = SelectedStore.Name;
        double openHour = SelectedStore.OpenTime?.TimeOfDay.TotalHours ?? 9;
        double closeHour = SelectedStore.CloseTime?.TimeOfDay.TotalHours ?? 17;
        int totalHours = (int)(closeHour - openHour);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"{storeName} - Weekly Schedule").FontSize(18).Bold().AlignCenter();
                    col.Item().PaddingBottom(8);
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(45); // Day column
                        for (int i = 0; i < totalHours; i++)
                            cols.RelativeColumn(); // Hour columns
                    });

                    // Header row with hours
                    table.Header(header =>
                    {
                        header.Cell().Background("#2d2d2d").Border(0.5f).BorderColor("#3d3d3d").Padding(4)
                            .AlignCenter().AlignMiddle().Text("").FontColor("#ffffff");

                        for (int h = 0; h < totalHours; h++)
                        {
                            int hour = (int)openHour + h;
                            string suffix = hour >= 12 ? "PM" : "AM";
                            int displayHour = hour % 12;
                            if (displayHour == 0) displayHour = 12;
                            string label = $"{displayHour}{suffix}";

                            header.Cell().Background("#2d2d2d").Border(0.5f).BorderColor("#3d3d3d").Padding(4)
                                .AlignCenter().AlignMiddle().Text(label).FontColor("#b0b0b0").FontSize(8);
                        }
                    });

                    // Day rows
                    for (int dayIdx = 0; dayIdx < Days.Count; dayIdx++)
                    {
                        var day = Days[dayIdx];
                        var rowBg = dayIdx % 2 == 0 ? "#f8f8f8" : "#ffffff";

                        // Day label cell
                        table.Cell().RowSpan(1).Background("#2d2d2d").Border(0.5f).BorderColor("#3d3d3d")
                            .Padding(4).AlignCenter().AlignMiddle()
                            .Text(day.DayName).FontColor("#b0b0b0").Bold().FontSize(10);

                        // Create a single merged cell for the timeline
                        table.Cell().ColumnSpan((uint)totalHours).Background(rowBg).Border(0.5f).BorderColor("#e0e0e0")
                            .MinHeight(50).Layers(layers =>
                            {
                                // Background grid lines for each hour
                                layers.Layer().Row(gridRow =>
                                {
                                    for (int h = 0; h < totalHours; h++)
                                    {
                                        gridRow.RelativeItem().BorderRight(0.5f).BorderColor("#e0e0e0");
                                    }
                                });

                                // Shift blocks layer
                                layers.PrimaryLayer().Padding(2).Column(shiftCol =>
                                {
                                    foreach (var shift in day.Shifts.OrderBy(s => s.StartHour))
                                    {
                                        // Calculate position as percentage
                                        double startPct = (shift.StartHour - openHour) / totalHours;
                                        double widthPct = (shift.EndHour - shift.StartHour) / totalHours;

                                        shiftCol.Item().PaddingVertical(1).Row(row =>
                                        {
                                            // Spacer for left offset
                                            if (startPct > 0)
                                                row.RelativeItem((float)(startPct * 100)).Text("");

                                            // Shift block
                                            row.RelativeItem((float)(widthPct * 100))
                                                .Background(shift.Color).Padding(3)
                                                .Column(blockCol =>
                                                {
                                                    blockCol.Item().Text(shift.EmployeeName)
                                                        .FontColor("#ffffff").Bold().FontSize(8);
                                                    blockCol.Item().Text(shift.TimeDisplay)
                                                        .FontColor("#ffffff").FontSize(7);
                                                });

                                            // Spacer for right
                                            double endPct = 1 - startPct - widthPct;
                                            if (endPct > 0)
                                                row.RelativeItem((float)(endPct * 100)).Text("");
                                        });
                                    }

                                    if (day.Shifts.Count == 0)
                                    {
                                        shiftCol.Item().AlignCenter().AlignMiddle().PaddingTop(15)
                                            .Text("No shifts").FontColor("#aaaaaa").Italic().FontSize(8);
                                    }
                                });
                            });
                    }
                });

                page.Footer().AlignCenter()
                    .Text($"Generated {DateTime.Now:g}").FontSize(8).FontColor("#888888");
            });
        });

        var fileName = $"{storeName}_Schedule.pdf";
        var filePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            fileName);

        document.GeneratePdf(filePath);
        SaveStatusMessage = $"Exported to Desktop";
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
    private int _empId;
    private double _start, _end;
    private string _color = "#4A90D9";

    public string EmployeeName { get => _empName; set { _empName = value; Notify(); } }
    public int EmployeeId { get => _empId; set { _empId = value; Notify(); } }
    public double StartHour { get => _start; set { _start = value; Notify(); Notify(nameof(TimeDisplay)); } }
    public double EndHour { get => _end; set { _end = value; Notify(); Notify(nameof(TimeDisplay)); } }
    public string Color { get => _color; set { _color = value; Notify(); } }

    public string TimeDisplay => $"{Fmt(StartHour)} - {Fmt(EndHour)}";
    public double Duration => EndHour - StartHour;

    static string Fmt(double h)
    {
        int hour = (int)h;
        int min = (int)((h - hour) * 60);
        string suffix = hour >= 12 ? "PM" : "AM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        return min == 0 ? $"{displayHour} {suffix}" : $"{displayHour}:{min:D2} {suffix}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([System.Runtime.CompilerServices.CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
