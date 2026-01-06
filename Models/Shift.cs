namespace Models;

public class Shift
{
    public int Id { get; set; }
    public string? Position { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Employee? AssignedEmployee { get; set; }
}
