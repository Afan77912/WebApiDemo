namespace WebApiDemo.DTOs
{
    public class EmployeeWithDepartmentDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }

        public string? DepartmentName { get; set; }
    }
}
