using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ShiftSchedulerDesktop.ViewModels;

namespace ShiftSchedulerDesktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new MainWindowViewModel();
    }
}
