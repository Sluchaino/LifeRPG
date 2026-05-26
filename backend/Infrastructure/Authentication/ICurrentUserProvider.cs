using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Authentication
{
    public interface ICurrentUserProvider
    {
        Guid? GetUserId(ClaimsPrincipal principal);
    }
}
