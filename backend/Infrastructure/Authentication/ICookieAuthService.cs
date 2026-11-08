using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Authentication
{
    public interface ICookieAuthService
    {
        Task SignInAsync(HttpContext httpContext, User user);
        Task SignOutAsync(HttpContext httpContext);
    }
}
