using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Data;
using BakeryPOS.API.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace BakeryPOS.API.Tests
{
    [Collection("SharedTestCollection")]
    public class ProductsControllerTests : IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private HttpClient _client;

        public ProductsControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // Runs BEFORE each test
        public async Task InitializeAsync()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                // Seed the admin user needed for authentication
                var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
                if (!await dbContext.Users.AnyAsync(u => u.Username == "admin"))
                {
                    dbContext.Users.Add(new User
                    {
                        Username = "admin",
                        PasswordHash = passwordService.HashPassword("password"),
                        FullName = "Test Admin",
                        Permissions = UserPermissions.Admin
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        // Runs AFTER each test
        public async Task DisposeAsync()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureDeletedAsync();
            }
        }

        private async Task AuthenticateAsAdminAsync()
        {
            var authClient = _factory.CreateClient();
            var response = await authClient.PostAsJsonAsync("/api/auth/login", new UserForLoginDto { Username = "admin", Password = "password" });
            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        }

        [Fact]
        public async Task GetProducts_WhenNoProductsExist_ReturnsEmptyList()
        {
            // ACT
            var response = await _client.GetAsync("/api/products");

            // ASSERT
            response.EnsureSuccessStatusCode();
            var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
            Assert.Empty(products);
        }

        [Fact]
        public async Task CreateProduct_AsAdmin_ReturnsCreated()
        {
            // ARRANGE
            await AuthenticateAsAdminAsync();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // The 'Uncategorized' category with ID=1 is not seeded in-memory, so we add it.
                await db.Categories.AddAsync(new Category { Id = 1, Name = "Test Category" });
                await db.SaveChangesAsync();
            }

            var newProduct = new ProductForCreateDto
            {
                Name = "Chocolate Cake",
                Price = 25.50m,
                CostPrice = 10.00m,
                StockQuantity = 50,
                CategoryId = 1,
                IsSpecial = false
            };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/products", newProduct);

            // ASSERT
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdProduct = await response.Content.ReadFromJsonAsync<ProductDto>();
            Assert.NotNull(createdProduct);
            Assert.Equal("Chocolate Cake", createdProduct.Name);
        }

        [Fact]
        public async Task CreateProduct_AsUnauthenticated_ReturnsUnauthorized()
        {
            // ARRANGE
            var newProduct = new ProductForCreateDto { Name = "Unauth Cake", Price = 10, CategoryId = 1 };

            // ACT
            var response = await _client.PostAsJsonAsync("/api/products", newProduct);

            // ASSERT
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}