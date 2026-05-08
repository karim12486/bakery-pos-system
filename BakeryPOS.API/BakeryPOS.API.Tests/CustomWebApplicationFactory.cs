using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BakeryPOS.API.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Test-only key — must be >=64 bytes for HMAC-SHA512.
                    ["AppSettings:TokenKey"] = new string('k', 64),
                    ["AppSettings:TokenIssuer"] = "BakeryPOS.API",
                    ["AppSettings:TokenAudience"] = "BakeryPOS.Client",
                    ["AppSettings:TokenLifetimeHours"] = "12"
                });
            });
        }
    }
}
