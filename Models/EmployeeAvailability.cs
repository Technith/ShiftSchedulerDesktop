namespace Models;

public class EmployeeAvailability
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int StoreId { get; set; }
    public Store? Store { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
}
