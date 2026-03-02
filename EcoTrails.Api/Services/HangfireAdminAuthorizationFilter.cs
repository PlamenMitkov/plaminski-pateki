using Hangfire.Dashboard;

namespace EcoTrails.Api.Services;

public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var host = httpContext.Request.Host.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("Admin");
    }
}
