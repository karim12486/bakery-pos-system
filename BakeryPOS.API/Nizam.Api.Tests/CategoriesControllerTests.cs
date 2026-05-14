using Nizam.Api.Core.Entities;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace Nizam.Api.Tests
{
    [Collection("SharedTestCollection")]
    public class CategoriesControllerTests : IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public CategoriesControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // This method runs BEFORE every test in this class
        public async Task InitializeAsync()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Ensure the database is created (for in-memory this is all that's needed)
                await dbContext.Database.EnsureCreatedAsync();
            }
        }

        // This method runs AFTER every test in this class
        public async Task DisposeAsync()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Ensure the database is deleted, giving us a clean slate for the next test
                await dbContext.Database.EnsureDeletedAsync();
            }
        }

        [Fact]
        public async Task GetCategories_ReturnsSuccessAndListOfCategories()
        {
            // ARRANGE: Add only the data needed for this specific test
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Categories.AddAsync(new Category { Name = "Cakes" });
                await db.SaveChangesAsync();
            }

            // ACT
            var response = await _client.GetAsync("/api/categories");

            // ASSERT
            response.EnsureSuccessStatusCode();
            var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();

            Assert.NotNull(categories);
            Assert.Single(categories); // Now this will work, as only "Cakes" was added
            Assert.Equal("Cakes", categories[0].Name);
        }
    }
}