using Microsoft.EntityFrameworkCore;
using WebApiDemo.DTOs;
using WebApiDemo.Models;
using WebApiDemo.Repositories;

namespace WebApiDemo.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _repository;

        public EmployeeService(IEmployeeRepository repository)
        {
            _repository = repository;
        }

        public async Task<EmployeeListResponseDto> GetAllAsync(
            string? department = null,
            string? search = null,
            string? sortBy = "id",
            string? sortOrder = "asc",
            int pageNumber = 1,
            int pageSize = 10)
        {
            if (pageNumber < 1)
                pageNumber = 1;

            if (pageSize < 1)
                pageSize = 10;

            var query = _repository.GetAll();

            // Filtering
            if (!string.IsNullOrEmpty(department))
            {
                query = query.Where(e => e.Department == department);
            }

            // Search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(e =>
                    e.Name.Contains(search) ||
                    e.Department.Contains(search));
            }

            // Sorting
            if (sortBy?.ToLower() == "name")
            {
                query = sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(e => e.Name)
                    : query.OrderBy(e => e.Name);
            }
            else if (sortBy?.ToLower() == "age")
            {
                query = sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(e => e.Age)
                    : query.OrderBy(e => e.Age);
            }
            else
            {
                query = sortOrder?.ToLower() == "desc"
                    ? query.OrderByDescending(e => e.Id)
                    : query.OrderBy(e => e.Id);
            }

            // Total records
            var totalRecords = await query.CountAsync();

            // Pagination + Projection
            var employees = await query
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    Age = e.Age,
                    Email = e.Email,
                    Phone = e.Phone
                })
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Total pages
            var totalPages = (int)Math.Ceiling(
                (double)totalRecords / pageSize);

            return new EmployeeListResponseDto
            {
                Success = true,
                Message = "Employees retrieved successfully.",
                Data = employees,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages
            };
        }

        public async Task<EmployeeDto?> GetByIdAsync(int id)
        {
            var employee = await _repository.GetByIdAsync(id);

            if (employee == null)
                return null;

            return new EmployeeDto
            {
                Id = employee.Id,
                Name = employee.Name,
                Department = employee.Department,
                Age = employee.Age,
                Email = employee.Email,
                Phone = employee.Phone
            };
        }

        public async Task<EmployeeDto> CreateAsync(
            CreateEmployeeDto employeeDto)
        {
            var employee = new Employee
            {
                Name = employeeDto.Name,
                Department = employeeDto.Department,
                Age = employeeDto.Age,
                Email = employeeDto.Email,
                Phone = employeeDto.Phone
            };

            await _repository.CreateAsync(employee);

            return new EmployeeDto
            {
                Id = employee.Id,
                Name = employee.Name,
                Department = employee.Department,
                Age = employee.Age,
                Email = employee.Email,
                Phone = employee.Phone
            };
        }

        public async Task<EmployeeDto?> UpdateAsync(
            int id,
            UpdateEmployeeDto employeeDto)
        {
            var employee = await _repository.GetByIdAsync(id);

            if (employee == null)
                return null;

            employee.Name = employeeDto.Name;
            employee.Department = employeeDto.Department;
            employee.Age = employeeDto.Age;
            employee.Email = employeeDto.Email;
            employee.Phone = employeeDto.Phone;

            await _repository.UpdateAsync(employee);

            return new EmployeeDto
            {
                Id = employee.Id,
                Name = employee.Name,
                Department = employee.Department,
                Age = employee.Age,
                Email = employee.Email,
                Phone = employee.Phone
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var employee = await _repository.GetByIdAsync(id);

            if (employee == null)
                return false;

            await _repository.DeleteAsync(employee);

            return true;
        }

        public async Task<List<EmployeeDto>> GetByDepartmentAsync(
            string department)
        {
            var employees = await _repository
                .GetByDepartmentAsync(department);

            return employees
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    Age = e.Age,
                    Email = e.Email,
                    Phone = e.Phone
                })
                .ToList();
        }

        public async Task<List<EmployeeDto>> GetSortedByNameAsync()
        {
            var employees = await _repository
                .GetSortedByNameAsync();

            return employees
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    Age = e.Age,
                    Email = e.Email,
                    Phone = e.Phone
                })
                .ToList();
        }

        public async Task<List<string>> GetAllNamesAsync()
        {
            var names = await _repository.GetAllNamesAsync();

            return names;
        }

        public async Task<int> GetCountAsync()
        {
            var count = await _repository.GetCountAsync();

            return count;
        }
    }
}