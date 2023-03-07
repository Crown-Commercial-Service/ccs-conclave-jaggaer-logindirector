using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using logindirector.Constants;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Rollbar;

namespace logindirector.Helpers
{
    public class UserHelpers : IHelpers
    {
        public IConfiguration _configuration { get; }
        public IMemoryCache _memoryCache;

        public UserHelpers(IConfiguration configuration, IMemoryCache memoryCache)
        {
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public bool HasValidUserRoles(AdaptorUserModel userModel, RequestSessionModel requestSessionModel)
        {
            // Check whether the user has a valid role / domain configuration for this application via both coreRoles and additionalRoles, and session request object
            if (userModel.coreRoles != null && userModel.coreRoles.Any())
            {
                if ((requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_CatUser) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.coreRoles.FirstOrDefault(r => r.roleKey == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid core role / domain configuration found - return true
                    return true;
                }
            }

            if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
            {
                if ((requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:CatDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_CatUser) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerBuyer) != null) ||
                    (requestSessionModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain") && userModel.additionalRoles.FirstOrDefault(r => r == AppConstants.RoleKey_JaeggerSupplier) != null))
                {
                    // Valid additional role / domain configuration found - return true
                    return true;
                }
            }

            // No valid role / domain configuration found for this user - return false
            return false;
        }

        public ErrorViewModel BuildErrorModelForUser(string sessionUserRequestJson)
        {
            // An error has occurred processing the user's request and we need an ErrorViewModel to represent that in various views.  Spool one up
            ErrorViewModel model = new ErrorViewModel
            {
                DashboardUrl = _configuration.GetValue<string>("DashboardPath")
            };

            if (!String.IsNullOrWhiteSpace(sessionUserRequestJson))
            {
                RequestSessionModel storedRequestModel = JsonConvert.DeserializeObject<RequestSessionModel>(sessionUserRequestJson);

                if (storedRequestModel != null && !String.IsNullOrWhiteSpace(storedRequestModel.domain))
                {
                    model.Service = new ServiceViewModel();

                    if (storedRequestModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain"))
                    {
                        // Looks like a Jaegger request
                        model.Service.ServiceDisplayName = AppConstants.Display_JaeggerServiceName;
                    }
                    else
                    {
                        // Must be a CaT request
                        model.Service.ServiceDisplayName = AppConstants.Display_CatServiceName;
                    }
                }
            }

            return model;
        }

        public async Task<bool> DoesUserHaveValidSession(HttpContext httpContext, string userSid)
        {
            // Use the user session ID to lookup an entry in the central cache
            if (!string.IsNullOrWhiteSpace(userSid))
            {
                List<UserSessionModel> sessionsList = new List<UserSessionModel>();
                string cacheKey = AppConstants.CentralCache_Key;

                if (_memoryCache.TryGetValue(cacheKey, out sessionsList))
                {
                    // We've got the cache - filter out any expired entries then check for our entry
                    sessionsList = sessionsList.Where(p => p.sessionStart > DateTime.Now.AddMinutes(-15)).ToList();
                    _memoryCache.Set(cacheKey, sessionsList);

                    UserSessionModel userCacheEntry = sessionsList.FirstOrDefault(p => p.sessionId == userSid);

                    if (userCacheEntry != null)
                    {
                        // User has a valid entry in the central cache - return true
                        return true;
                    }
                }
            }

            // No valid user detected - expire their authentication if they have any, then return false
            try
            {
                httpContext.Session.Clear();
                await httpContext.SignOutAsync("CookieAuth");
            }
            catch (Exception ex)
            {
                // Log error
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return false;
        }
    }
}
