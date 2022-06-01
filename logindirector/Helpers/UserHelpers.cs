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

        public bool HasValidUserRoles(AdaptorUserModel userModel)
        {
            // Check whether the user has a valid role for this application
            if (userModel.coreRoles != null && userModel.coreRoles.Any())
            {
                List<AdaptorUserRoleModel> relevantRoles = userModel.coreRoles.Where(r => r.serviceClientName == AppConstants.Adaptor_ClientRoleAssignment).ToList();

                if (relevantRoles != null && relevantRoles.Any())
                {
                    // Valid role found - return true
                    return true;
                }
            }

            // Also check against additionalRoles incase a valid role has been added by means of a usergroup
            if (userModel.additionalRoles != null && userModel.additionalRoles.Any())
            {
                List<string> relevantRoles = userModel.additionalRoles.Where(r => r == AppConstants.RoleKey_JaeggerSupplier || r == AppConstants.RoleKey_JaeggerBuyer || r == AppConstants.RoleKey_CatUser || r == AppConstants.RoleKey_CatAdmin).ToList();

                if (relevantRoles != null && relevantRoles.Any())
                {
                    // Valid role found - return true
                    return true;
                }
            }

            // No valid roles found for this user - return false
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
                RollbarLocator.RollbarInstance.Error("Triggering user logout");

                httpContext.Session.Clear();
                await httpContext.SignOutAsync("CookieAuth");
            }
            catch (Exception ex)
            {
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return false;
        }
    }
}
