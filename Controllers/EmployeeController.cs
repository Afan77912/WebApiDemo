using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using WebApiDemo.Data;
using WebApiDemo.DTOs;
using WebApiDemo.Exceptions;
using WebApiDemo.Models;
using WebApiDemo.Repositories;
using WebApiDemo.Services;
using System.Text.Json;


namespace WebApiDemo.Controllers
{
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EmployeeController> _logger;
        private readonly IEmployeeService _employeeService;
        private readonly UserService _userService;
        private readonly IMemoryCache _cache;
        private readonly IDistributedCache _redisCache;
        private const string EmployeeCacheVersionKey =
            "employee_cache_version";

        public EmployeeController(
            ILogger<EmployeeController> logger,
            AppDbContext context,
            IEmployeeService employeeService,
            UserService userService,
            IMemoryCache cache,
            IDistributedCache redisCache)
        {
            _logger = logger;
            _context = context;
            _employeeService = employeeService;
            _userService = userService;
            _cache = cache;
            _redisCache = redisCache;
        }

        // =========================================================
        // CACHE HELPER METHODS
        // =========================================================

        private int GetEmployeeCacheVersion()
        {
            if (!_cache.TryGetValue(
                    EmployeeCacheVersionKey,
                    out int version))
            {
                version = 1;

                _cache.Set(
                    EmployeeCacheVersionKey,
                    version);
            }

            return version;
        }

        private void InvalidateEmployeeCache()
        {
            var currentVersion = GetEmployeeCacheVersion();

            _cache.Set(
                EmployeeCacheVersionKey,
                currentVersion + 1);

            _logger.LogInformation(
                "Employee cache invalidated. New version: {CacheVersion}",
                currentVersion + 1);
        }

        // =========================================================
        // ADMIN ONLY
        // =========================================================

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly()
        {
            return Ok(new
            {
                Message =
                    "Welcome Admin! You can access this endpoint."
            });
        }

        // =========================================================
        // RATE LIMITING TEST
        // =========================================================

        /// <summary>
        /// Rate limiting test endpoint.
        /// This endpoint is protected by a fixed window rate limiter.
        /// </summary>
        [EnableRateLimiting("fixed")]
        [HttpGet("rate-limit-test")]
        public IActionResult RateLimitTest()
        {
            return Ok(new
            {
                message =
                    "Rate limiting test successful."
            });
        }

        // =========================================================
        // CLAIM TEST
        // =========================================================

        [Authorize(Policy = "CanViewEmployees")]
        [HttpGet("claim-test")]
        public IActionResult ClaimTest()
        {
            return Ok(new
            {
                Message =
                    "You have the ViewEmployees permission."
            });
        }

        // =========================================================
        // ADMIN EMPLOYEE ACCESS
        // =========================================================

        [Authorize(Policy = "AdminEmployeeAccess")]
        [HttpGet("admin-employee-access")]
        public IActionResult AdminEmployeeAccess()
        {
            return Ok(new
            {
                Message =
                    "Admin employee access granted."
            });
        }

        // =========================================================
        // ADMIN SECURE TEST
        // =========================================================

        [Authorize(Policy = "AdminEmployeeAccess")]
        [HttpGet("admin-secure-test")]
        public IActionResult AdminSecureTest()
        {
            return Ok(new
            {
                success = true,
                message =
                    "Admin authorization successful."
            });
        }

        // =========================================================
        // CUSTOM ADMIN ACCESS
        // =========================================================

        [Authorize(Policy = "CustomAdminAccess")]
        [HttpGet("custom-admin-access")]
        public IActionResult CustomAdminAccess()
        {
            return Ok(new
            {
                Message =
                    "Custom authorization handler passed successfully."
            });
        }

        // =========================================================
        // AS NO TRACKING TEST
        // =========================================================

        [HttpGet("readonly-test/{id}")]
        public async Task<IActionResult> ReadOnlyTest(int id)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            employee.Name =
                employee.Name + " Changed";

            await _context.SaveChangesAsync();

            return Ok(employee);
        }

        // =========================================================
        // GET ALL EMPLOYEES
        // WITH CACHE
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Get(
            string? department = null,
            string? search = null,
            string? sortBy = "id",
            string? sortOrder = "asc",
            int pageNumber = 1,
            int pageSize = 10)
        {
            _logger.LogInformation(
                "Getting employees. PageNumber: {PageNumber}, PageSize: {PageSize}",
                pageNumber,
                pageSize);

            // Get current cache version
            var cacheVersion =
                GetEmployeeCacheVersion();

            // Create dynamic cache key
            var cacheKey =
                $"employees_v{cacheVersion}_{department}_{search}_{sortBy}_{sortOrder}_{pageNumber}_{pageSize}";

            // Check Redis cache
            var cachedJson = await _redisCache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedResult =
                    JsonSerializer.Deserialize<EmployeeListResponseDto>(
                        cachedJson);

                _logger.LogInformation(
                    "Redis Cache HIT: {CacheKey}",
                    cacheKey);

                return Ok(cachedResult);
            }

            _logger.LogInformation(
                "Redis Cache MISS: {CacheKey}",
                cacheKey);

           // Get data from service
            var result =
                await _employeeService.GetAllAsync(
                    department,
                    search,
                    sortBy,
                    sortOrder,
                    pageNumber,
                    pageSize);

            // Save result in Redis
            var json = JsonSerializer.Serialize(result);

            await _redisCache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            return Ok(result);
        }


        [HttpGet("redis-test")]
        public async Task<IActionResult> RedisTest()
        {
            await _redisCache.SetStringAsync(
                "redis_test",
                "Hello from Redis!");

            var value = await _redisCache.GetStringAsync("redis_test");

            return Ok(new
            {
                message = value
            });
        }

        // =========================================================
        // GET EMPLOYEE WITH DEPARTMENT
        // =========================================================

        [HttpGet("with-department/{id}")]
        public async Task<IActionResult> GetWithDepartment(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.DepartmentDetails)
                .Where(e => e.Id == id)
                .Select(e => new EmployeeWithDepartmentDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    DepartmentId = e.DepartmentId,
                    DepartmentName =
                        e.DepartmentDetails != null
                            ? e.DepartmentDetails.Name
                            : null
                })
                .FirstOrDefaultAsync();

            if (employee == null)
                return NotFound();

            return Ok(employee);
        }

        // =========================================================
        // GET EMPLOYEES WITH DEPARTMENTS
        // =========================================================

        [HttpGet("with-departments")]
        public async Task<IActionResult>
            GetEmployeesWithDepartments()
        {
            var employees = await _context.Employees
                .Include(e => e.DepartmentDetails)
                .Select(e => new EmployeeWithDepartmentDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    DepartmentId = e.DepartmentId,
                    DepartmentName =
                        e.DepartmentDetails != null
                            ? e.DepartmentDetails.Name
                            : null
                })
                .ToListAsync();

            return Ok(employees);
        }

        // =========================================================
        // DEPARTMENT WITH EMPLOYEES
        // =========================================================

        [HttpGet("department-with-employees/{id}")]
        public async Task<IActionResult>
            GetDepartmentWithEmployees(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Employees)
                .Where(d => d.Id == id)
                .Select(d => new DepartmentWithEmployeesDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Employees = d.Employees
                        .Select(e => e.Name)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (department == null)
                return NotFound();

            return Ok(department);
        }

        // =========================================================
        // EXPLICIT LOADING
        // =========================================================

        [HttpGet("explicit-department/{id}")]
        public async Task<IActionResult>
            GetEmployeeWithExplicitDepartment(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            await _context.Entry(employee)
                .Reference(e => e.DepartmentDetails)
                .LoadAsync();

            return Ok(new EmployeeWithDepartmentDto
            {
                Id = employee.Id,
                Name = employee.Name,
                Department = employee.Department,
                DepartmentId = employee.DepartmentId,
                DepartmentName =
                    employee.DepartmentDetails?.Name
            });
        }

        // =========================================================
        // PROJECTION
        // =========================================================

        [HttpGet("projection")]
        public async Task<IActionResult>
            GetEmployeesProjection()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    Age = e.Age,
                    Email = e.Email,
                    Phone = e.Phone
                })
                .ToListAsync();

            return Ok(employees);
        }

        // =========================================================
        // IQUERYABLE TEST
        // =========================================================

        [HttpGet("queryable-test")]
        public async Task<IActionResult> QueryableTest()
        {
            IQueryable<Employee> query =
                _context.Employees;

            query = query.Where(
                e => e.Age >= 18);

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
                .ToListAsync();

            return Ok(employees);
        }

        // =========================================================
        // IENUMERABLE TEST
        // =========================================================

        [HttpGet("enumerable-test")]
        public async Task<IActionResult> EnumerableTest()
        {
            IEnumerable<Employee> employees =
                await _context.Employees
                    .ToListAsync();

            var result = employees
                .Where(e => e.Age >= 18)
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

            return Ok(result);
        }

        // =========================================================
        // PAGINATION
        // =========================================================

        [HttpGet("pagination")]
        public async Task<IActionResult>
            GetEmployeesPagination(
                int page = 1,
                int pageSize = 5,
                string? department = null,
                string? sortBy = "id",
                string? sortOrder = "asc")
        {
            if (page < 1 || pageSize < 1)
                return BadRequest(
                    "Page and pageSize must be greater than 0.");

            var query = _context.Employees
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(
                    e => e.Department == department);
            }

            var totalRecords =
                await query.CountAsync();

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

            var employees = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    Department = e.Department,
                    Age = e.Age,
                    Email = e.Email,
                    Phone = e.Phone
                })
                .ToListAsync();

            var totalPages =
                (int)Math.Ceiling(
                    (double)totalRecords / pageSize);

            return Ok(new
            {
                data = employees,
                currentPage = page,
                pageSize,
                totalRecords,
                totalPages,
                hasNextPage =
                    page < totalPages,
                hasPreviousPage =
                    page > 1
            });
        }

        // =========================================================
        // GET BY ID
        // =========================================================

        [HttpGet("{id}")]
        [ProducesResponseType(
            typeof(ApiResponse<EmployeeDto>),
            StatusCodes.Status200OK)]
        [ProducesResponseType(
            typeof(ErrorResponse),
            StatusCodes.Status404NotFound)]
        public async Task<ActionResult<EmployeeDto>>
            GetById(int id)
        {
            _logger.LogInformation(
                "Getting employee with ID {EmployeeId}.",
                id);

            var result =
                await _employeeService.GetByIdAsync(id);

            if (result == null)
            {
                _logger.LogWarning(
                    "Employee with ID {EmployeeId} was not found.",
                    id);

                throw new NotFoundException(
                    $"Employee with id {id} not found.");
            }

            return Ok(new ApiResponse<EmployeeDto>
            {
                Success = true,
                Message =
                    "Employee retrieved successfully.",
                Data = result
            });
        }

        // =========================================================
        // CREATE EMPLOYEE
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> Create(
            CreateEmployeeDto employeeDto)
        {
            _logger.LogInformation(
                "Creating a new employee.");

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result =
                await _employeeService.CreateAsync(
                    employeeDto);

            // IMPORTANT:
            // Employee list cache is now outdated.
            InvalidateEmployeeCache();

            _logger.LogInformation(
                "Employee created successfully with ID {EmployeeId}.",
                result.Id);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }

        // =========================================================
        // REGISTER USER
        // =========================================================

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult>
            Register(RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x =>
                        x.Value!.Errors.Count > 0)
                    .Select(x => new
                    {
                        Field = x.Key,
                        Errors = x.Value!.Errors
                            .Select(e =>
                                e.ErrorMessage)
                            .ToList()
                    })
                    .ToList();

                return BadRequest(new
                {
                    Message = "Validation failed.",
                    Errors = errors
                });
            }

            if (registerDto.Password.Contains(
                    "password",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "Password cannot contain the word 'password'.");
            }

            var existingUser =
                await _userService
                    .GetByUsernameAsync(
                        registerDto.Username);

            if (existingUser != null)
            {
                throw new BadRequestException(
                    "Username already exists.");
            }

            var user =
                new WebApiDemo.Models.User
                {
                    Username = registerDto.Username,
                    Role = "User"
                };

            user.Password =
                await _userService.HashPasswordAsync(
                    user,
                    registerDto.Password);

            await _userService.CreateUserAsync(user);

            return Ok(new
            {
                Message =
                    "User registered successfully."
            });
        }

        // =========================================================
        // UPDATE EMPLOYEE
        // =========================================================

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            UpdateEmployeeDto employeeDto)
        {
            _logger.LogInformation(
                "Updating employee with ID {EmployeeId}.",
                id);

            var result =
                await _employeeService.UpdateAsync(
                    id,
                    employeeDto);

            if (result == null)
            {
                _logger.LogWarning(
                    "Employee with ID {EmployeeId} was not found for update.",
                    id);

                return NotFound();
            }

            // IMPORTANT:
            // Employee list cache is now outdated.
            InvalidateEmployeeCache();

            _logger.LogInformation(
                "Employee with ID {EmployeeId} updated successfully.",
                id);

            return Ok(result);
        }

        // =========================================================
        // TRACKING TEST
        // =========================================================

        [HttpPut("tracking-test/{id}")]
        public async Task<IActionResult>
            TrackingTest(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
                return NotFound();

            employee.Name =
                employee.Name + " Updated";

            await _context.SaveChangesAsync();

            // Employee data changed,
            // so invalidate employee list cache.
            InvalidateEmployeeCache();

            return Ok(employee);
        }

        // =========================================================
        // DELETE EMPLOYEE
        // =========================================================

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            _logger.LogInformation(
                "Deleting employee with ID {EmployeeId}.",
                id);

            var deleted =
                await _employeeService.DeleteAsync(id);

            if (!deleted)
            {
                _logger.LogWarning(
                    "Employee with ID {EmployeeId} was not found for deletion.",
                    id);

                return NotFound();
            }

            // IMPORTANT:
            // Employee list cache is now outdated.
            InvalidateEmployeeCache();

            _logger.LogInformation(
                "Employee with ID {EmployeeId} deleted successfully.",
                id);

            return NoContent();
        }

        // =========================================================
        // GET BY DEPARTMENT
        // =========================================================

        [HttpGet("department/{deptName}")]
        public async Task<IActionResult>
            GetByDepartment(string deptName)
        {
            var employees =
                await _employeeService
                    .GetByDepartmentAsync(
                        deptName);

            return Ok(employees);
        }

        // =========================================================
        // SORTED EMPLOYEES
        // =========================================================

        [HttpGet("sorted")]
        public async Task<IActionResult>
            GetSortedByName()
        {
            var employees =
                await _employeeService
                    .GetSortedByNameAsync();

            return Ok(employees);
        }

        // =========================================================
        // ALL NAMES
        // =========================================================

        [HttpGet("names")]
        public async Task<IActionResult>
            GetAllNames()
        {
            var names =
                await _employeeService
                    .GetAllNamesAsync();

            return Ok(names);
        }

        // =========================================================
        // COUNT
        // =========================================================

        [HttpGet("count")]
        public async Task<IActionResult> GetCount()
        {
            var count =
                await _employeeService
                    .GetCountAsync();

            return Ok(new
            {
                TotalEmployees = count
            });
        }
    }
}