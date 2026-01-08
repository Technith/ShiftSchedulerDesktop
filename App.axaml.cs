using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;

namespace ShiftSchedulerDesktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Ensure new tables are created
        using (var ctx = new DatabaseContext())
        {
            ctx.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS AvailabilityTable (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeId INTEGER NOT NULL,
                    StoreId INTEGER NOT NULL,
                    DayOfWeek INTEGER NOT NULL,
                    StartTime TEXT,
                    EndTime TEXT,
                    FOREIGN KEY (EmployeeId) REFERENCES EmployeeTable(Id),
                    FOREIGN KEY (StoreId) REFERENCES StoreTable(Id)
                )
            ");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
