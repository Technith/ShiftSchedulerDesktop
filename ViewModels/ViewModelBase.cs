using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ShiftSchedulerDesktop.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Notify([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    protected bool SetProperty<T>(ref T field, T val, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, val)) return false;
        field = val;
        Notify(p);
        return true;
    }
}
