using System.Windows.Input;

namespace ShiftSchedulerDesktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;

    public EmployeesViewModel EmployeesVM { get; }
    public StoresViewModel StoresVM { get; }
    public CalendarViewModel CalendarVM { get; }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public ICommand NavigateEmployeesCommand { get; }
    public ICommand NavigateStoresCommand { get; }
    public ICommand NavigateCalendarCommand { get; }

    public MainWindowViewModel()
    {
        using var context = new DatabaseContext();
        context.Database.EnsureCreated();

        EmployeesVM = new EmployeesViewModel();
        StoresVM = new StoresViewModel();
        CalendarVM = new CalendarViewModel(StoresVM);

        _currentPage = EmployeesVM;

        NavigateEmployeesCommand = new RelayCommand(() => CurrentPage = EmployeesVM);
        NavigateStoresCommand = new RelayCommand(() => CurrentPage = StoresVM);
        NavigateCalendarCommand = new RelayCommand(() => CurrentPage = CalendarVM);
    }
}
