using Nizam.Api.Core.Enums;

namespace Nizam.Api.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserPermissions Permissions { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ImageUrl { get; set; }
        public string? Role { get; set; }
    }
}