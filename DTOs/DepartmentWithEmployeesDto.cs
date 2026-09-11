namespace WebApiDemo.DTOs
{
    public class DepartmentWithEmployeesDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<string> Employees { get; set; } = new();
    }
}