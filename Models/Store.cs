namespace Models;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public List<Shift> Schedule { get; set; } = new();
    public List<Employee> Employees { get; set; } = new();

    public override string ToString() => Name;
}
