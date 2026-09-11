using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using WebApiDemo.Authorization;
using WebApiDemo.Data;
using WebApiDemo.Middleware;
using WebApiDemo.Repositories;
using WebApiDemo.Services;
using Microsoft.AspNetCore.Identity;
using WebApiDemo.Models;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddMemoryCache();

using (var scope = builder.Services.BuildServiceProvider().CreateScope())
{
    var hasher = new PasswordHasher<User>();

    var user = new User
    {
        Username = "Afan",
        Role = "Admin"
    };

    var hash = hasher.HashPassword(user, "Afan@123456");

    Console.WriteLine("PASSWORD HASH:");
    Console.WriteLine(hash);
}

// Add Controllers + Validation Response
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .Select(x => new
                {
                    Field = x.Key,
                    Errors = x.Value.Errors
                        .Select(e => e.ErrorMessage)
                        .ToList()
                })
                .ToList();

            return new BadRequestObjectResult(new
            {
                Message = "Validation failed.",
                Errors = errors
            });
        };
    });

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis:6379";
    options.InstanceName = "WebApiDemo_";
});
// =============================
// Rate Limiting
// =============================
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.QueueLimit = 0;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                Success = false,
                Message = "Too many requests. Please try again later."
            },
            cancellationToken);
    };
});

// =============================
// Authentication - JWT
// =============================
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtSettings["Key"]!))
        };
    });


// =============================
// Authorization Policies
// =============================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewEmployees", policy =>
    {
        policy.RequireClaim("Permission", "ViewEmployees");
    });

    options.AddPolicy("AdminEmployeeAccess", policy =>
    {
        policy.RequireRole("Admin");
        policy.RequireClaim("Permission", "ViewEmployees");
    });

    options.AddPolicy("CustomAdminAccess", policy =>
    {
        policy.Requirements.Add(
            new AdminEmployeeRequirement());
    });
});


// =============================
// Custom Authorization Handler
// =============================
builder.Services.AddSingleton<
    IAuthorizationHandler,
    AdminEmployeeHandler>();


// =============================
// API Versioning
// =============================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});


// =============================
// Database
// =============================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));


// =============================
// Repository
// =============================
builder.Services.AddScoped<
    IEmployeeRepository,
    EmployeeRepository>();


// =============================
// Service
// =============================
builder.Services.AddScoped<
    IEmployeeService,
    EmployeeService>();


// =============================
// User Repository & Service
// =============================
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<UserService>();


// =============================
// JWT Service
// =============================
builder.Services.AddScoped<JwtService>();


// =============================
// CORS
// =============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// =============================
// Swagger
// =============================
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Name = "Authorization",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter JWT Bearer token"
        });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                    "Bearer",
                    document),
                new List<string>()
            }
        });
});


var app = builder.Build();


// =============================
// HTTP Request Pipeline
// =============================
app.MapOpenApi();

app.UseSwagger();

app.UseSwaggerUI();

// =============================
// HTTPS
// =============================
app.UseHttpsRedirection();


// =============================
// Global Exception Middleware
// =============================
app.UseMiddleware<ExceptionMiddleware>();


app.UseMiddleware<SecurityHeadersMiddleware>();

// =============================
// CORS
// =============================
app.UseCors("AllowFrontend");


// =============================
// Authentication
// =============================
app.UseAuthentication();


// =============================
// Authorization
// =============================
app.UseAuthorization();


// =============================
// Rate Limiting
// =============================
app.UseRateLimiter();


// =============================
// Controllers
// =============================
app.MapControllers();


// =============================
// Run Application
// =============================
app.Run();

// Git workflow practice