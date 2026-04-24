using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Authentication
{
    public sealed class PasswordService : IPasswordService
    {
        private readonly PasswordHasher<User> _passwordHasher = new();

        public string HashPassword(User user, string password)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
        {
            var result = _passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
