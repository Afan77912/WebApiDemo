using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WebApiDemo.Authorization
{
    public class AdminEmployeeHandler
        : AuthorizationHandler<AdminEmployeeRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminEmployeeRequirement requirement)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

            var permission = context.User
                .FindFirst("Permission")?.Value;

            if (role == "Admin" &&
                permission == "ViewEmployees")
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
