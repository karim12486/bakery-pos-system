using Nizam.Api.Core.Enums;

namespace Nizam.Api.DTOs
{
    public class UserDetailDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Permissions { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Role { get; set; }
        public string? ImageUrl { get; set; }
    }
}