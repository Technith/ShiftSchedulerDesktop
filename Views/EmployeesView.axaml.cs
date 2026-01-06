using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ShiftSchedulerDesktop.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
