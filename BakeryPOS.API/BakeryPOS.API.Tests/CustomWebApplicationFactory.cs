using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BakeryPOS.API.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        // Static ctor runs ONCE per AppDomain, before any WebApplication.CreateBuilder is invoked.
        // Setting these as environment variables means the very first call to
        // WebApplication.CreateBuilder sees them via the default env-var configuration provider —
        // which is essential because Program.cs validates AppSettings:TokenKey at startup
        // (before WebApplicationFactory.ConfigureAppConfiguration callbacks are applied).
        static CustomWebApplicationFactory()
        {
            // Test-only key — must be >=64 bytes for HMAC-SHA512 validation in AuthenticationExtensions.
            Environment.SetEnvironmentVariable("AppSettings__TokenKey", new string('k', 64));
            Environment.SetEnvironmentVariable("AppSettings__TokenIssuer", "BakeryPOS.API");
            Environment.SetEnvironmentVariable("AppSettings__TokenAudience", "BakeryPOS.Client");
            Environment.SetEnvironmentVariable("AppSettings__TokenLifetimeHours", "12");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
