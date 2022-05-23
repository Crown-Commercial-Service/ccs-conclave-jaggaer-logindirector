﻿using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace logindirector.Helpers
{
    public interface IHelpers
    {
        bool HasValidUserRoles(AdaptorUserModel userModel);

        ErrorViewModel BuildErrorModelForUser(string sessionUserRequestJson);

        Task<bool> DoesUserHaveValidSession(HttpContext httpContext, string userEmail);
    }
}
