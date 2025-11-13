using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace BakeryPOS.API.Tests
{
    [Collection("SharedTestCollection")]
    public class AdminControllerTests : IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AdminControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // This runs BEFORE each test
        public async Task InitializeAsync()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Wipe and recreate the REAL database using migrations
                await dbContext.Database.EnsureDeletedAsync();
                await dbContext.Database.MigrateAsync();
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task AuthenticateAsAdminAsync()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new UserForLoginDto { Username = "admin", Password = "password" });
            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        }

        [Fact]
        public async Task CreateUser_AsAdmin_ReturnsCreated()
        {
            // ARRANGE
            await AuthenticateAsAdminAsync();
            var newUser = new UserForCreationDto
            {
                Username = "cashier1",
                Password = "password123",
                FullName = "Test Cashier",
                Permissions = Core.Enums.UserPermissions.Cashier
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/admin/users", newUser);

            // ASSERT
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }
}