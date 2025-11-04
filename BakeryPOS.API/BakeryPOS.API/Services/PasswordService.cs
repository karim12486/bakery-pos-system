using BakeryPOS.API.Core.Interfaces;

namespace BakeryPOS.API.Services
{
    public class PasswordService : IPasswordService
    {
        public string HashPassword(string password)
        {
            // BCrypt.Net will generate a salt and hash the password
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            // This will compare the provided password with the stored hash
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }
}