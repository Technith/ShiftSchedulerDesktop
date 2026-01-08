using Microsoft.EntityFrameworkCore;
using Models;

public class DatabaseContext : DbContext
{
    public DbSet<Employee>? EmployeeTable { get; set; }
    public DbSet<Shift>? ShiftTable { get; set; }
    public DbSet<Store>? StoreTable { get; set; }
    public DbSet<EmployeeAvailability>? AvailabilityTable { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shiftscheduler.db");
        options.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure many-to-many relationship between Store and Employee
        modelBuilder.Entity<Store>()
            .HasMany(s => s.Employees)
            .WithMany(e => e.Stores);

        // Configure EmployeeAvailability relationships
        modelBuilder.Entity<EmployeeAvailability>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId);

        modelBuilder.Entity<EmployeeAvailability>()
            .HasOne(a => a.Store)
            .WithMany()
            .HasForeignKey(a => a.StoreId);
    }
}
