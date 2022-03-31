using System;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Rollbar;
using logindirector.Models.AdaptorService;
using logindirector.Models.TendersApi;
using logindirector.Services;
using logindirector.Helpers;
using logindirector.Constants;
using logindirector.Models;
using Microsoft.Extensions.Configuration;

// Controller to handle all user processing actions done by the application, before outgoing requests are applied
namespace logindirector.Controllers
{
    public class UserProcessingController : Controller
    {
        public IAdaptorClientServices _adaptorClientServices;
        public ITendersClientServices _tendersClientServices;
        public IHelpers _userHelpers;
        public IMemoryCache _memoryCache;
        public IConfiguration _configuration { get; }

        public UserProcessingController(IAdaptorClientServices adaptorClientServices, ITendersClientServices tendersClientServices, IHelpers userHelpers, IMemoryCache memoryCache, IConfiguration configuration)
        {
            _adaptorClientServices = adaptorClientServices;
            _tendersClientServices = tendersClientServices;
            _userHelpers = userHelpers;
            _memoryCache = memoryCache;
            _configuration = configuration;
        }

        // Route to process all users logging into the system - account interactions in Jaegger / CaT, and store the data we need for later
        [HttpGet]
        [Route("/director/process-user", Order = 1)]
        [Authorize]
        public IActionResult ProcessUser()
        {
            // First of all, make sure we have the user email in claims and then use it to fetch a user model from the Adaptor service
            string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                // User appears to be successfully authenticated with SSO service - so fetch their user data from the adaptor service
                AdaptorUserModel userModel = _adaptorClientServices.GetUserInformation(userEmail).Result;

                if (userModel != null && _userHelpers.HasValidUserRoles(userModel))
                {
                    // Serialise the model as JSON and store it in the session
                    HttpContext.Session.SetString(AppConstants.Session_UserKey, JsonConvert.SerializeObject(userModel));

                    // Then add a record for this user to the central session cache
                    AddUserToCentralSessionCache(userModel);

                    // Now access the Tenders API to work out whether this user needs a account merge/creation or just forwarding
                    string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value;

                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        UserStatusModel userStatusModel = _tendersClientServices.GetUserStatus(userEmail, accessToken).Result;

                        if (userStatusModel != null)
                        {
                            // Now we have a user status response, work out what to do with the user
                            if (userStatusModel.UserStatus == AppConstants.Tenders_UserStatus_ActionRequired)
                            {
                                // The user needs to either merge or create a Jaegger account - display the merge prompt
                                return View("~/Views/Merging/MergePrompt.cshtml");
                            }
                            else if (userStatusModel.UserStatus == AppConstants.Tenders_UserStatus_AlreadyMerged)
                            {
                                // User is already merged, so we're good here - send the user to have their initial request processed
                                return RedirectToAction("ActionRequest", "Request");
                            }
                        }
                    }
                }
                else
                {
                    // User is not permitted to use the Login Director - log error, and present error
                    RollbarLocator.RollbarInstance.Error("Attempted access by unauthorised SSO user - " + userEmail);

                    return View("~/Views/Errors/Unauthorised.cshtml");
                }
            }

            // If we've got to here, the user isn't properly authenticated or the Tenders API gave us an error response, so display a generic error
            return View("~/Views/Errors/Generic.cshtml");
        }

        // Route to process user selection at the Merge Prompt
        [HttpPost]
        [Route("/director/process-user", Order = 1)]
        [Authorize]
        public IActionResult ProcessUserMergeSelection(string accountDecision)
        {
            if (accountDecision == "merge")
            {
                // User wants to merge their account
                // TODO: Real action here when flow determined (redirect to Jaegger login probably?)
            }
            else
            {
                // User wants to create a new account
                string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value;

                if (!string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(accessToken))
                {
                    UserCreationModel userCreationModel = _tendersClientServices.CreateJaeggerUser(userEmail, accessToken).Result;

                    if (userCreationModel != null)
                    {
                        // Now we have a user creation response, work out what to do with the user next
                        if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_Success)
                        {
                            // User account has been created - now we can proceed to action their initial request
                            return RedirectToAction("ActionRequest", "Request");
                        }
                        else if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_Failure)
                        {
                            // There's been an issue creating the user's account.  Therefore, display a failure page related to a creation failure
                            return View("~/Views/Errors/CreateError.cshtml");
                        }
                    }
                }
            }

            // If we've got to here, the user isn't properly authenticated or the Tenders API gave us a generic error response, so display a generic error
            return View("~/Views/Errors/Generic.cshtml");
        }

        // Route to receive users back from Jaegger once their account has been merged
        [HttpGet]
        [Route("/director/account-linked", Order = 1)]
        [Authorize]
        public IActionResult ContinueProcessingMergedUser()
        {
            // Firstly, since the user is coming back from an external service make sure their session with us still exists - if it doesn't, we can't continue
            string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
            string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value;

            // TODO: Should this check be changed to their request details once we have those in session?
            if (!string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(accessToken))
            {
                // User session seems to still exist.  User can now be sent on to process their original request
                return RedirectToAction("ActionRequest", "Request");
            }

            // If we've gotten to here the user session no longer appears to be in a correct state (likely timed out and lost details of the original request) - display a session expired notice
            ErrorViewModel model = new ErrorViewModel
            {
                DashboardUrl = _configuration.GetValue<string>("DashboardPath")
            };

            return View("~/Views/Errors/SessionExpired.cshtml", model);
        }

        // Adds an entry for an authenticated user into the central session cache
        internal void AddUserToCentralSessionCache(AdaptorUserModel userModel)
        {
            if (userModel != null && !String.IsNullOrWhiteSpace(userModel.emailAddress))
            {
                // A single entry in the cache needs to contain the user's email, and an entry timestamp
                List<UserSessionModel> sessionsList;
                string cacheKey = AppConstants.CentralCache_Key;

                if (_memoryCache.TryGetValue(cacheKey, out sessionsList))
                {
                    // The cache already has entries - filter out any expired ones
                    sessionsList = sessionsList.Where(p => p.sessionStart > DateTime.Now.AddMinutes(-30)).ToList();
                }
                else
                {
                    // No existing entries in the cache, so start it fresh
                    sessionsList = new List<UserSessionModel>();
                }

                // Now add a new entry for ourselves
                UserSessionModel userEntry = new UserSessionModel
                {
                    userEmail = userModel.emailAddress,
                    sessionStart = DateTime.Now
                };

                sessionsList.Add(userEntry);

                // Set the newly amended list back into the cache
                _memoryCache.Set(cacheKey, sessionsList);
            }
        }
    }
}
