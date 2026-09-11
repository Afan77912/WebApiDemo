using WebApiDemo.DTOs;

namespace WebApiDemo.Services
{
    public interface IEmployeeService
    {
        Task<EmployeeListResponseDto> GetAllAsync(
            string? department = null,
            string? search = null,
            string? sortBy = "id",
            string? sortOrder = "asc",
            int pageNumber = 1,
            int pageSize = 10);

        Task<EmployeeDto?> GetByIdAsync(int id);

        Task<EmployeeDto> CreateAsync(
            CreateEmployeeDto employeeDto);

        Task<EmployeeDto?> UpdateAsync(
            int id,
            UpdateEmployeeDto employeeDto);

        Task<bool> DeleteAsync(int id);

        Task<List<EmployeeDto>> GetByDepartmentAsync(
            string department);

        Task<List<EmployeeDto>> GetSortedByNameAsync();

        Task<List<string>> GetAllNamesAsync();

        Task<int> GetCountAsync();
    }
}