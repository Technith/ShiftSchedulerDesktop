namespace Models;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public List<Store> Stores { get; set; } = new();

    public override string ToString() => $"{Name}\t{Phone}";
}
