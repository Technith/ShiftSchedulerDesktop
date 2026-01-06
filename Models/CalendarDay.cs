using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShiftSchedulerDesktop.Models;

public class CalendarDay : INotifyPropertyChanged
{
    DateTime _date;
    bool _curMonth, _dragOver;

    public DateTime Date { get => _date; set { _date = value; Notify(); } }
    public bool IsCurrentMonth { get => _curMonth; set { _curMonth = value; Notify(); } }
    public bool IsDragOver { get => _dragOver; set { _dragOver = value; Notify(); } }
    public ObservableCollection<ShiftItem> Shifts { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ShiftItem : INotifyPropertyChanged
{
    string _id = Guid.NewGuid().ToString();
    string _empName = "";
    TimeSpan _start, _end;
    string _color = "#4A90D9";

    public string Id { get => _id; set { _id = value; Notify(); } }
    public string EmployeeName { get => _empName; set { _empName = value; Notify(); } }
    public TimeSpan StartTime { get => _start; set { _start = value; Notify(); } }
    public TimeSpan EndTime { get => _end; set { _end = value; Notify(); } }
    public string Color { get => _color; set { _color = value; Notify(); } }
    public string TimeDisplay => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
