using logindirector.Constants;
using logindirector.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Rollbar;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace logindirector.Controllers
{
    // Controller to hold all session related functionality - primarily related to the Backchannel Logout system
    public class SessionController : Controller
    {
        public IConfiguration _configuration { get; }
        public IMemoryCache _memoryCache;

        public SessionController(IConfiguration configuration, IMemoryCache memoryCache)
        {
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        [Route("/director/rpiframe", Order = 1)]
        public IActionResult BackchannelRpIframe()
        {
            BackchannelModel model = new BackchannelModel
            {
                ClientId = _configuration.GetValue<string>("SsoService:ClientId"),
                RedirectUrl = Request.Host.Host + "/director/process-user",
                SecurityApiUrl = _configuration.GetValue<string>("SsoService:SsoDomain"),
                SessionState = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Hash)?.Value
        };

            return PartialView("~/Views/Backchannel/RpIframe.cshtml", model);
        }

        // Route to handle PPG backchannel logout requests
        [HttpPost]
        [Route("/logout", Order = 1)]
        public IActionResult BackchannelLogout(string logout_token)
        {
            // We've been passed a JWT token as part of this request, which contains the session ID.  We need to decode that, then use it to identify and close down the relevant session
            if (!string.IsNullOrWhiteSpace(logout_token))
            {
                try
                {
                    JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                    JwtSecurityToken tokenValues = handler.ReadJwtToken(logout_token);

                    if (tokenValues != null)
                    {
                        Claim sessionIdClaim = tokenValues.Claims.FirstOrDefault(p => p.Type == "sid");

                        if (sessionIdClaim != null && !string.IsNullOrWhiteSpace(sessionIdClaim.Value))
                        {
                            // TEMP logging for backchannel debug
                            RollbarLocator.RollbarInstance.Error("User SID from Backchannel is - " + sessionIdClaim.Value);

                            // We have the session ID, so now just find and expire them from the central cache
                            RemoveUserFromCentralSessionCache(sessionIdClaim.Value);

                            // User should now be logged out, so return an OK response
                            return StatusCode(200);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Something has gone wrong extracting the session ID from the JWT token passed to us.  We need to log this as an error
                    RollbarLocator.RollbarInstance.Error(ex);
                }
            }

            // If we've got this far, we've had an issue with what's been passed to us.  Return a Bad Request response and do nothing
            RollbarLocator.RollbarInstance.Error("Backchannel Logout error - unhandled error");
            return StatusCode(400);
        }

        internal void RemoveUserFromCentralSessionCache(string sessionId)
        {
            // We need to expire any entries in the central session cache that have the provided session ID
            List<UserSessionModel> sessionsList;
            string cacheKey = AppConstants.CentralCache_Key;

            if (_memoryCache.TryGetValue(cacheKey, out sessionsList))
            {
                // TEMP logging for backchannel debug
                RollbarLocator.RollbarInstance.Error("triggering logout for SID - " + sessionId + " - Entries before - " + sessionsList.Count);

                sessionsList = sessionsList.Where(p => p.sessionId != sessionId).ToList();

                RollbarLocator.RollbarInstance.Error("Sessions count after logout - " + sessionsList.Count);
            }

            // We should now have a filtered list without entries with the specified session ID, so set it back into the cache
            _memoryCache.Set(cacheKey, sessionsList);
        }
    }
}
