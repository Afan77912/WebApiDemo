using WebApiDemo.Models;

namespace WebApiDemo
{
    public class Employee
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Department { get; set; }

        public int Age { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; } = string.Empty;

        public int? DepartmentId { get; set; }

        public Department? DepartmentDetails { get; set; }
    }
}