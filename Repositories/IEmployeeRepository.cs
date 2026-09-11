using WebApiDemo.Models;

namespace WebApiDemo.Repositories
{
    public interface IEmployeeRepository
    {
        IQueryable<Employee> GetAll();

        Task<Employee?> GetByIdAsync(int id);

        Task<Employee> CreateAsync(Employee employee);

        Task UpdateAsync(Employee employee);

        Task DeleteAsync(Employee employee);

        Task<List<Employee>> GetByDepartmentAsync(string department);

        Task<List<Employee>> GetSortedByNameAsync();

        Task<List<string>> GetAllNamesAsync();

        Task<int> GetCountAsync();
    }
}
