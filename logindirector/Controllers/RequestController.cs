using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using logindirector.Constants;
using logindirector.Models;

// Controller to handle all incoming and outgoing requests to and from the application
namespace logindirector.Controllers
{
    public class RequestController : Controller
    {
        public IMemoryCache _memoryCache;

        public RequestController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        // Catch all route which all incoming requests - order set to 999 to ensure fixed routes supercede it
        [Route("{*url}", Order = 999)]
        public IActionResult Index()
        {
            // TODO (in other cases): Store details of incoming request in session so we have it for later on

            // We need to check to see if the user has already been logged in via the Login Director earlier
            string userSessionData = HttpContext.Session.GetString(AppConstants.Session_UserKey);

            if (!string.IsNullOrWhiteSpace(userSessionData))
            {
                // There is user data in session, so the user has already been logged in.  Send them to the Request Processing endpoint (we'll validate session there)
                return RedirectToAction("ActionRequest", "Request");
            }
            else
            {
                // There's no user data in session.  This is the user's first trip to Login Director this session, so send them to the Process User endpoint
                return RedirectToAction("ProcessUser", "UserProcessing");
            }
        }

        // Route to process and execute outgoing requests, once a user has been logged in and processed
        [Route("/director/action-request", Order = 1)]
        [Authorize]
        public IActionResult ActionRequest()
        {
            // First, check to see that the user's session is valid in the central cache
            if (DoesUserHaveValidSession())
            {
                // User appears to be valid, so now we can process their request
                // TODO (in other case): Delete view and do work here instead
                return View("~/Views/Home/ProcessRequest.cshtml");
            }
            else
            {
                // User does not appear to have a valid session in the central cache - clear them down and send them to re-authenticate at the Process User endpoint
                HttpContext.Session.Clear();

                return RedirectToAction("ProcessUser", "UserProcessing");
            }
        }

        internal bool DoesUserHaveValidSession()
        {
            // Grab the user email address from our claims, and then use it to lookup an entry in the central cache
            string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                List<UserSessionModel> sessionsList = new List<UserSessionModel>();
                string cacheKey = AppConstants.CentralCache_Key;

                if (_memoryCache.TryGetValue(cacheKey, out sessionsList))
                {
                    // We've got the cache - filter out any expired entries then check for our entry
                    sessionsList = sessionsList.Where(p => p.sessionStart > DateTime.Now.AddMinutes(-30)).ToList();
                    _memoryCache.Set(cacheKey, sessionsList);

                    UserSessionModel userCacheEntry = sessionsList.FirstOrDefault(p => p.userEmail == userEmail);

                    if (userCacheEntry != null)
                    {
                        // User has a valid entry in the central cache - return true
                        return true;
                    }
                }
            }

            // No valid user detected - return false
            return false;
        }





        // TODO: Change this to a proper error setup later
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
