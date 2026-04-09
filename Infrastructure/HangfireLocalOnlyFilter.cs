// Infrastructure/HangfireLocalOnlyFilter.cs
using Hangfire.Dashboard;

namespace Xllerate.Infrastructure;

public class HangfireLocalOnlyFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Allow only local requests in development
        return httpContext.Connection.LocalIpAddress?
                .Equals(httpContext.Connection.RemoteIpAddress) ?? false
            || httpContext.Request.Host.Host == "localhost";
    }
}