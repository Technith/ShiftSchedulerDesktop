using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Models;
using ShiftSchedulerDesktop.ViewModels;

namespace ShiftSchedulerDesktop.Views;

public partial class CalendarView : UserControl
{
    const double HourWidth = 70;
    const double ShiftH = 50;
    const double ShiftGap = 4;
    const double ShiftPad = 4;

    CalendarViewModel? VM => DataContext as CalendarViewModel;

    bool _resizing;
    bool _resizeLeft;
    ScheduleShift? _resizeShift;
    ScheduleDay? _resizeDay;
    double _resizeX0;
    int _origStart, _origEnd;

    public CalendarView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        LayoutUpdated += (_, _) => PositionAllShifts();
    }

    void PositionAllShifts()
    {
        if (VM == null) return;

        var rows = this.FindControl<ItemsControl>("DayRowsControl");
        if (rows == null) return;

        int open = VM.SelectedStore?.OpenTime?.Hour ?? 9;

        foreach (var ctrl in rows.GetVisualDescendants().OfType<ItemsControl>())
        {
            if (ctrl == rows || ctrl.DataContext is not ScheduleDay day) continue;

            var canvas = ctrl.GetVisualDescendants().OfType<Canvas>().FirstOrDefault();
            if (canvas == null) continue;

            var items = canvas.Children
                .OfType<ContentPresenter>()
                .Where(p => p.DataContext is ScheduleShift)
                .Select(p => (p, (ScheduleShift)p.DataContext!))
                .ToList();

            var rowMap = CalcRows(items.Select(x => x.Item2).ToList());
            int maxRow = rowMap.Count > 0 ? rowMap.Values.Max() + 1 : 1;

            foreach (var (pres, shift) in items)
            {
                int row = rowMap.GetValueOrDefault(shift, 0);
                double left = (shift.StartHour - open) * HourWidth;
                double top = ShiftPad + row * (ShiftH + ShiftGap);

                Canvas.SetLeft(pres, left);
                Canvas.SetTop(pres, top);
                pres.Width = Math.Max((shift.EndHour - shift.StartHour) * HourWidth - 4, 50);
            }

            double h = ShiftPad + maxRow * (ShiftH + ShiftGap) + ShiftPad;
            canvas.MinHeight = ctrl.MinHeight = Math.Max(60, h);
        }
    }

    Dictionary<ScheduleShift, int> CalcRows(List<ScheduleShift> shifts)
    {
        var result = new Dictionary<ScheduleShift, int>();
        if (shifts.Count == 0) return result;

        var sorted = shifts.OrderBy(s => s.StartHour).ThenBy(s => s.EndHour);
        var ends = new List<int>();

        foreach (var s in sorted)
        {
            int row = ends.FindIndex(e => s.StartHour >= e);
            if (row >= 0) ends[row] = s.EndHour;
            else { row = ends.Count; ends.Add(s.EndHour); }
            result[s] = row;
        }
        return result;
    }

    async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (VM == null) return;

        var el = e.Source as Control;
        var border = FindParent<Border>(el, b => b.DataContext is ScheduleShift);

        if (border?.DataContext is ScheduleShift shift)
        {
            var day = FindDayContext(border);
            if (day == null) return;

            double rx = e.GetPosition(border).X;
            double w = border.Bounds.Width;

            if (rx <= 10) { BeginResize(shift, day, true, e); e.Handled = true; }
            else if (rx >= w - 10) { BeginResize(shift, day, false, e); e.Handled = true; }
            else
            {
                VM.StartShiftDrag(day, shift);
                var d = new DataObject();
                d.Set("Shift", shift);
                d.Set("SourceDay", day);
                await DragDrop.DoDragDrop(e, d, DragDropEffects.Move);
                VM.CancelDrag();
            }
            return;
        }

        var empBorder = FindParent<Border>(el, b => b.DataContext is Employee);
        if (empBorder?.DataContext is Employee emp)
        {
            VM.StartEmployeeDrag(emp);
            var d = new DataObject();
            d.Set("Employee", emp);
            var res = await DragDrop.DoDragDrop(e, d, DragDropEffects.Copy);
            if (res == DragDropEffects.None) VM.CancelDrag();
        }
    }

    void BeginResize(ScheduleShift s, ScheduleDay day, bool left, PointerPressedEventArgs e)
    {
        _resizing = true;
        _resizeLeft = left;
        _resizeShift = s;
        _resizeDay = day;
        _resizeX0 = e.GetPosition(this).X;
        _origStart = s.StartHour;
        _origEnd = s.EndHour;
        e.Pointer.Capture((IInputElement)e.Source!);
    }

    void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_resizing || _resizeShift == null || VM == null) return;

        double dx = e.GetPosition(this).X - _resizeX0;
        int dh = (int)Math.Round(dx / HourWidth);
        int open = VM.SelectedStore?.OpenTime?.Hour ?? 9;
        int close = VM.SelectedStore?.CloseTime?.Hour ?? 17;

        if (_resizeLeft)
            _resizeShift.StartHour = Math.Clamp(_origStart + dh, open, _origEnd - 1);
        else
            _resizeShift.EndHour = Math.Clamp(_origEnd + dh, _origStart + 1, close);

        PositionAllShifts();
    }

    void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        _resizeShift = null;
        _resizeDay = null;
        e.Pointer.Capture(null);
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("Employee") && !e.Data.Contains("Shift"))
        { e.DragEffects = DragDropEffects.None; return; }

        e.DragEffects = e.Data.Contains("Employee") ? DragDropEffects.Copy : DragDropEffects.Move;
        VM?.SetDragOver(FindDayContext(e.Source as Control));
    }

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (VM == null) return;

        if (IsOverBank(e.Source) && e.Data.Contains("Shift") && VM.DraggedShift != null && VM.DragSource != null)
        {
            VM.DeleteShift(VM.DragSource, VM.DraggedShift);
            VM.CancelDrag();
            PositionAllShifts();
            return;
        }

        var day = FindDayContext(e.Source as Control);
        if (day == null) { VM.CancelDrag(); return; }

        int hour = GetDropHour(e);

        if (e.Data.Contains("Employee") && VM.DraggedEmployee != null)
        {
            VM.DropOnSchedule(day, hour);
            e.DragEffects = DragDropEffects.Copy;
        }
        else if (e.Data.Contains("Shift") && VM.DraggedShift != null)
        {
            VM.DropOnSchedule(day, hour);
            e.DragEffects = DragDropEffects.Move;
        }
        PositionAllShifts();
    }

    bool IsOverBank(object? src)
    {
        var el = src as Control;
        var bank = this.FindControl<Border>("EmployeeBankBorder");
        while (el != null) { if (el == bank) return true; el = el.Parent as Control; }
        return false;
    }

    int GetDropHour(DragEventArgs e)
    {
        if (VM == null) return 9;
        int open = VM.SelectedStore?.OpenTime?.Hour ?? 9;
        int close = VM.SelectedStore?.CloseTime?.Hour ?? 17;

        var el = e.Source as Control;
        Canvas? cv = el as Canvas;
        while (cv == null && el != null)
        {
            cv = el.GetVisualDescendants().OfType<Canvas>().FirstOrDefault();
            el = el?.Parent as Control;
        }
        if (cv == null) return open;

        int off = (int)(e.GetPosition(cv).X / HourWidth);
        return Math.Clamp(open + off, open, close - 1);
    }

    ScheduleDay? FindDayContext(Control? el)
    {
        while (el != null) { if (el.DataContext is ScheduleDay d) return d; el = el.Parent as Control; }
        return null;
    }

    T? FindParent<T>(Control? el, Func<T, bool>? pred = null) where T : Control
    {
        while (el != null) { if (el is T t && (pred == null || pred(t))) return t; el = el.Parent as Control; }
        return null;
    }
}
