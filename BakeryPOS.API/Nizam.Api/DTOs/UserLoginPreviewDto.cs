namespace Nizam.Api.DTOs
{
    public class UserLoginPreviewDto
    {
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}