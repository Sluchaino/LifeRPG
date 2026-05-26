using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Authentication
{
    public interface IPasswordService
    {
        string HashPassword(User user, string password);
        bool VerifyPassword(User user, string hashedPassword, string providedPassword);
    }
}
