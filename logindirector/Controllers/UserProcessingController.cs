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
using System.Threading.Tasks;

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
        public async Task<IActionResult> ProcessUserAsync()
        {
            // We need to make sure the user has a valid session before we do anything.  They do, UNLESS this isn't their first request this session and there's no central cache entry
            bool validSession = true;
            string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value,
                userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value,
                requestDetails = HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

            if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString(AppConstants.Session_UserPreAuthenticated)))
            {
                // This isn't the user's first request this session, so check whether they still have a session in the central cache
                if (!await _userHelpers.DoesUserHaveValidSession(HttpContext, userSid))
                {
                    // User does not have a valid session in the central cache.  Backchannel logout related - take note of this
                    validSession = false;
                }
            }

            if (validSession && !String.IsNullOrWhiteSpace(requestDetails))
            {
                // Set a session value to indicate that the user is as yet unprocessed
                HttpContext.Session.SetString(AppConstants.Session_ProcessingRequiredKey, "true");

                // First of all, make sure we have the user email in claims and then use it to fetch a user model from the Adaptor service
                if (!string.IsNullOrWhiteSpace(userEmail))
                {
                    // User appears to be successfully authenticated with SSO service - so fetch their user data from the adaptor service
                    AdaptorUserModel userModel = await _adaptorClientServices.GetUserInformation(userEmail);
                    RequestSessionModel requestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestDetails);

                    if (userModel != null && requestModel != null && _userHelpers.HasValidUserRoles(userModel, requestModel.domain))
                    {
                        // Serialise the model as JSON and store it in the session
                        HttpContext.Session.SetString(AppConstants.Session_UserKey, JsonConvert.SerializeObject(userModel));

                        // Then add a record for this user to the central session cache, and mark them as pre-authenticated
                        AddUserToCentralSessionCache(userModel);
                        HttpContext.Session.SetString(AppConstants.Session_UserPreAuthenticated, "true");

                        // Now access the Tenders API to work out whether this user needs a account merge/creation or just forwarding
                        string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value;

                        if (!string.IsNullOrWhiteSpace(accessToken))
                        {
                            UserStatusModel userStatusModel = await _tendersClientServices.GetUserStatus(userEmail, accessToken, requestModel.domain);

                            if (userStatusModel != null)
                            {
                                // Now we have a user status response, work out what to do with the user
                                if (userStatusModel.UserStatus == AppConstants.Tenders_UserStatus_ActionRequired)
                                {
                                    // The user needs to either merge or create a Jaegger / CaT account - display the merge prompt
                                    ServiceViewModel model = GetServiceViewModelForRequest(userModel);

                                    return View("~/Views/Merging/MergePrompt.cshtml", model);
                                }
                                else if (userStatusModel.UserStatus == AppConstants.Tenders_UserStatus_Unauthorised)
                                {
                                    // The user is not authorised to use the service - display the unauthorised message
                                    ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                                    return View("~/Views/Errors/Unauthorised.cshtml", model);
                                }
                                else if (userStatusModel.UserStatus == AppConstants.Tenders_UserStatus_Conflict)
                                {
                                    // There is a role mismatch for the user between PPG and Jaegger / CaT.  Display the role mismatch error message
                                    ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                                    return View("~/Views/Errors/RoleConflict.cshtml", model);
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

                        ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                        return View("~/Views/Errors/Unauthorised.cshtml", model);
                    }
                }

                // If we've got to here, the user isn't properly authenticated or the Tenders API gave us an error response, so display a generic error
                ErrorViewModel errorModel = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/Generic.cshtml", errorModel);
            }
            else
            {
                // User doesn't have a valid session, likely meaning they were backchannel logout'd.  Display session expiry
                ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/SessionExpired.cshtml", model);
            }
        }

        // Route to process user selection at the Merge Prompt
        [HttpPost]
        [Route("/director/process-user", Order = 1)]
        [Authorize]
        public async Task<IActionResult> ProcessUserMergeSelectionAsync(string accountDecision)
        {
            // Before we do anything, we need to validate that the user still has an active session (i.e. there's not been a backchannel logout
            string userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value;

            if (!String.IsNullOrWhiteSpace(userSid) && await _userHelpers.DoesUserHaveValidSession(HttpContext, userSid))
            {
                // User has a valid session in the central cache.  Proceed with request
                if (accountDecision == "merge")
                {
                    // User wants to merge their existing account - therefore, we need to send them off to Jaegger to perform one final authentication
                    string handbackUrl = "https://" + Request.Host.Host + _configuration.GetValue<string>("HandbackPath"),
                        externalAuthenticationUrl = _configuration.GetValue<string>("ExternalAuthenticationPath") + handbackUrl;
                    return Redirect(externalAuthenticationUrl);
                }
                else
                {
                    // User wants to create a new account
                    string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
                    string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value;

                    if (!string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(accessToken))
                    {
                        UserCreationModel userCreationModel = await _tendersClientServices.CreateJaeggerUser(userEmail, accessToken);

                        if (userCreationModel != null)
                        {
                            // Now we have a user creation response, work out what to do with the user next
                            if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_Success)
                            {
                                // User account has been created - now we can proceed to action their initial request
                                return RedirectToAction("ActionRequest", "Request");
                            }
                            else
                            {
                                ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));

                                if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_Conflict)
                                {
                                    // There is a role mismatch for the user between PPG and Jaegger / CaT.  Display the role mismatch error message
                                    return View("~/Views/Errors/RoleConflict.cshtml", model);
                                }
                                else if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_MissingRole)
                                {
                                    // The user is missing a Jaegger / CaT role in PPG.  Display the unauthorised message
                                    return View("~/Views/Errors/Unauthorised.cshtml", model);
                                }
                                else if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_HelpdeskRequired)
                                {
                                    // The user's PPG setup is incorrect (both roles assigned).  Display a message directing the user to the helpdesk
                                    return View("~/Views/Errors/BothRolesAssigned.cshtml", model);
                                }
                                else if (userCreationModel.CreationStatus == AppConstants.Tenders_UserCreation_AlreadyExists)
                                {
                                    // The user already exists in Jaegger / CaT.  Display the existing account message
                                    return View("~/Views/Errors/ExistingAccount.cshtml", model);
                                }
                            }
                        }
                    }
                }

                // If we've got to here, the user isn't properly authenticated or the Tenders API gave us a generic error response, so display a generic creation error
                ErrorViewModel errorModel = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/CreateError.cshtml", errorModel);
            }
            else
            {
                // User doesn't have a valid session, likely meaning they were backchannel logout'd.  Display session expiry
                ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/SessionExpired.cshtml", model);
            }
        }

        // Route to receive users back from Jaegger once their account has been merged
        [HttpGet]
        [Route("/director/account-linked", Order = 1)]
        [Authorize]
        public async Task<IActionResult> ContinueProcessingMergedUserAsync()
        {
            // Firstly, since the user is coming back from an external service make sure their session with us still exists - if it doesn't, we can't continue
            string userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value,
                   accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value,
                   userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value;

            if (!string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(accessToken) && await _userHelpers.DoesUserHaveValidSession(HttpContext, userSid))
            {
                // User is still in session - make sure we have their request details too though, else we've nothing to action
                string requestSessionData = HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

                if (!string.IsNullOrWhiteSpace(requestSessionData))
                {
                    RequestSessionModel storedRequestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestSessionData);

                    // We can check against any value in the model to confirm we still have the request details.  Just use the desired path here
                    if (storedRequestModel != null && !string.IsNullOrWhiteSpace(storedRequestModel.requestedPath))
                    {
                        // User session seems to still exist.  User can now be sent on to process their original request
                        return RedirectToAction("ActionRequest", "Request");
                    }
                }
            }

            // If we've gotten to here the user session no longer appears to be in a correct state (likely timed out and lost details of the original request) - display a session expired notice
            ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
            return View("~/Views/Errors/SessionExpired.cshtml", model);
        }

        // Adds an entry for an authenticated user into the central session cache
        internal void AddUserToCentralSessionCache(AdaptorUserModel userModel)
        {
            if (userModel != null && !String.IsNullOrWhiteSpace(userModel.emailAddress))
            {
                // A single entry in the cache needs to contain the user's email, an entry timestamp, and their session ID
                List<UserSessionModel> sessionsList;
                string cacheKey = AppConstants.CentralCache_Key;

                if (_memoryCache.TryGetValue(cacheKey, out sessionsList))
                {
                    // The cache already has entries - filter out any expired ones
                    sessionsList = sessionsList.Where(p => p.sessionStart > DateTime.Now.AddMinutes(-15)).ToList();
                }
                else
                {
                    // No existing entries in the cache, so start it fresh
                    sessionsList = new List<UserSessionModel>();
                }

                // Now the list has been updated, if there's no existing entry for the current user, we need to add a new entry
                UserSessionModel existingModel = sessionsList.FirstOrDefault(p => p.userEmail == userModel.emailAddress);

                if (existingModel == null)
                {
                    string userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value;

                    // Now add a new entry for ourselves
                    UserSessionModel userEntry = new UserSessionModel
                    {
                        userEmail = userModel.emailAddress,
                        sessionStart = DateTime.Now,
                        sessionId = userSid
                    };

                    sessionsList.Add(userEntry);
                }

                // Set the newly amended list back into the cache
                _memoryCache.Set(cacheKey, sessionsList);
            }
        }

        // Uses the request data stored in session to build a ServiceViewModel for use later in views
        internal ServiceViewModel GetServiceViewModelForRequest(AdaptorUserModel userModel)
        {
            ServiceViewModel model = new ServiceViewModel();

            // Get the request data from session
            string requestSessionData = HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

            if (!string.IsNullOrWhiteSpace(requestSessionData) && userModel != null)
            {
                RequestSessionModel storedRequestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestSessionData);

                if (storedRequestModel != null && !String.IsNullOrWhiteSpace(storedRequestModel.domain))
                {
                    if (storedRequestModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain"))
                    {
                        // Looks like a Jaegger request
                        model.ServiceDisplayName = AppConstants.Display_JaeggerServiceName;

                        // For Jaegger requests we need to check the additional roles in the userModel, so we know if we need to display an error message in the view
                        if (userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && !userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerSupplier))
                        {
                            model.ShowBuyerError = true;
                        }
                        else if (!userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerBuyer) && userModel.additionalRoles.Contains(AppConstants.RoleKey_JaeggerSupplier))
                        {
                            model.ShowSupplierError = true;
                        }
                    }
                    else
                    {
                        // Must be a CaT request
                        model.ServiceDisplayName = AppConstants.Display_CatServiceName;
                        model.ShowBuyerError = true;
                    }
                }
            }

            return model;
        }
    }
}
