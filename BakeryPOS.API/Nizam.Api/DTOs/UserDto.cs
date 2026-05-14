namespace Nizam.Api.DTOs
{
    public class UserDto
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; }
        public int Permissions { get; set; }
        public string? ImageUrl { get; set; }
    }
}