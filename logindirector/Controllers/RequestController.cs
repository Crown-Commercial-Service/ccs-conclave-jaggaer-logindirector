using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using logindirector.Constants;
using logindirector.Models;
using System.Runtime.CompilerServices;
using logindirector.Helpers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

// Controller to handle all incoming and outgoing requests to and from the application
[assembly: InternalsVisibleTo("LoginDirectorTests")]
namespace logindirector.Controllers
{
    public class RequestController : Controller
    {
        public IMemoryCache _memoryCache;
        public IConfiguration _configuration { get; }
        public IHelpers _userHelpers;

        public RequestController(IMemoryCache memoryCache, IConfiguration configuration, IHelpers userHelpers)
        {
            _memoryCache = memoryCache;
            _configuration = configuration;
            _userHelpers = userHelpers;
        }

        // Catch all route for all incoming requests EXCEPT the callback path - order set to 999 to ensure fixed routes supercede it
        [Route("{*url}", Order = 999)]
        public IActionResult Index()
        {
            // Before we do anything, make sure the request has come from an approved source
            if (isUserFromSupportedSource())
            {
                // Request is supported - we can proceed to process it.  Begin by storing the details of their request for later
                RequestSessionModel requestModel = getRequestModel();

                // Also check if this is their first visit to us in this session or not - we need to remember this for later if it's not
                if (User.Identity.IsAuthenticated)
                {
                    HttpContext.Session.SetString(AppConstants.Session_UserPreAuthenticated, "true");
                }
                else
                {
                    HttpContext.Session.Remove(AppConstants.Session_UserPreAuthenticated);
                }

                if (requestModel != null)
                {
                    if (requestModel.httpFormat.ToUpper() == "POST")
                    {
                        string requestedRoute;

                        // This is a POST request - don't do anything else, just forward the request on as is
                        if (requestModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain"))
                        {
                            // Jaegger requests for now are just forwarded to the core domain value
                            requestedRoute = requestModel.protocol + "://" + requestModel.domain;
                        }
                        else
                        {
                            // CAS requests are forwarded to the requested endpoint
                            requestedRoute = requestModel.protocol + "://" + requestModel.domain + requestModel.requestedPath;
                        }

                        return RedirectPreserveMethod(requestedRoute);
                    }
                    else
                    {
                        // This is a GET request - continue with application flow
                        storeRequestDetailsInSession(requestModel);

                        // We need to check to see if the user has already been logged in via the Login Director earlier, and whether they've made their processing decision
                        string userSessionData = HttpContext.Session.GetString(AppConstants.Session_UserKey),
                            userProcessingStr = HttpContext.Session.GetString(AppConstants.Session_ProcessingRequiredKey);

                        if (!string.IsNullOrWhiteSpace(userSessionData) && !string.IsNullOrWhiteSpace(userProcessingStr))
                        {
                            bool processingRequired = Convert.ToBoolean(userProcessingStr);

                            if (processingRequired)
                            {
                                // User has not yet completed their processing - send them back to the Process User endpoint
                                return RedirectToAction("ProcessUser", "UserProcessing");
                            }
                            else
                            {
                                // User has already been fully processed - send them to the Request Processing endpoint (we'll validate session there)
                                return RedirectToAction("ActionRequest", "Request");
                            }
                        }
                        else
                        {
                            // There's no user data in session.  This is the user's first trip to Login Director this session, so send them to the Process User endpoint
                            return RedirectToAction("ProcessUser", "UserProcessing");
                        }
                    }
                }
                else
                {
                    // There was an error fetching the request details - display our Generic error view
                    ErrorViewModel errorModel = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                    return View("~/Views/Errors/Generic.cshtml", errorModel);
                }
            }
            else
            {
                // Request appears to be from unsupported source.  Redirect the user to Conclave itself and remove them from this application flow
                return Redirect(_configuration.GetValue<string>("DashboardPath"));
            }
        }

        // Route to process and execute outgoing requests, once a user has been logged in and processed
        [Route("/director/action-request", Order = 1)]
        [Authorize]
        public async Task<IActionResult> ActionRequest()
        {
            // First, check to see that the user's session is valid in the central cache
            string userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value;

            if (await _userHelpers.DoesUserHaveValidSession(HttpContext, userSid))
            {
                // Update our session value to indicate that the user has now been processed within this session
                HttpContext.Session.SetString(AppConstants.Session_ProcessingRequiredKey, "false");

                // User appears to be valid, so now we can process their request from the stored request object
                string requestJson = HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

                if (!string.IsNullOrWhiteSpace(requestJson))
                {
                    RequestSessionModel requestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestJson);

                    if (requestModel != null)
                    {
                        // We've got the user's request details from session.  Now action them as a GET redirect (POSTs were handled earlier)
                        string requestedRoute;

                        if (requestModel.domain == _configuration.GetValue<string>("ExitDomains:JaeggerDomain"))
                        {
                            // Jaegger requests are always sent direct to a specific endpoint
                            requestedRoute = requestModel.protocol + "://" + requestModel.domain;
                        }
                        else
                        {
                            // CAS requests go to where the user requested
                            requestedRoute = requestModel.protocol + "://" + requestModel.domain + requestModel.requestedPath;
                        }

                        return Redirect(requestedRoute);
                    }
                }

                // If we've gotten this far there's been some kind of issue fetching the request details from session.  Display Session Expiry message
                ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/SessionExpired.cshtml", model);
            }
            else
            {
                // User does not appear to have a valid session in the central cache - clear them down and send them to re-authenticate at the Process User endpoint
                return RedirectToAction("ProcessUser", "UserProcessing");
            }
        }

        internal bool isUserFromSupportedSource()
        {
            // Default response should always be that the request is not from a supported source, unless proven otherwise
            bool isSupported = false;

            // We need to inspect the domain that the request is coming from and determine whether it's one of our supported sources
            if (Request?.Host != null && !string.IsNullOrWhiteSpace(Request.Host.Host))
            {
                string requestSource = Request.Host.Host.ToLower();
                List<string> supportedSources = new List<string>
                {
                    _configuration.GetValue<string>("SupportedSources:JaeggerSource"),
                    _configuration.GetValue<string>("SupportedSources:CatSource")
                };

                if (supportedSources.Contains(requestSource))
                {
                    // Request comes from a supported source
                    isSupported = true;
                }
            }

            return isSupported;
        }

        internal void storeRequestDetailsInSession(RequestSessionModel model)
        {
            // Store the details of the user's request in session so that we can action it later once the application flow is complete
            HttpContext.Session.SetString(AppConstants.Session_RequestDetailsKey, JsonConvert.SerializeObject(model));
        }

        internal RequestSessionModel getRequestModel()
        {
            RequestSessionModel model = null;

            // Build a model of the user's request
            if (Request != null && !string.IsNullOrWhiteSpace(Request.Host.Host) && !string.IsNullOrWhiteSpace(Request.Scheme) && !string.IsNullOrWhiteSpace(Request.Path) && !string.IsNullOrWhiteSpace(Request.Method))
            {
                model = new RequestSessionModel
                {
                    protocol = Request.Scheme,
                    requestedPath = Request.Path,
                    httpFormat = Request.Method
                };

                // The domain needs to be set to be the correct exit domain based on the domain the user came in via
                if (Request.Host.Host.ToLower() == _configuration.GetValue<string>("SupportedSources:JaeggerSource"))
                {
                    // Jaegger domain request
                    model.domain = _configuration.GetValue<string>("ExitDomains:JaeggerDomain");
                }
                else
                {
                    // We already know it's an approved source request at this point, and there are only two approved sources, so this must be from the CaT domain
                    model.domain = _configuration.GetValue<string>("ExitDomains:CatDomain");
                }
            }

            return model;
        }

        // Fixed unauthorised route - we need this setting up as fixed display too, to serve the middleware
        [Route("/director/unauthorised", Order = 1)]
        public IActionResult Unauthorised()
        {
            ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));

            return View("~/Views/Errors/Unauthorised.cshtml", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
            model.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            return View("~/Views/Errors/Generic.cshtml", model);
        }
    }
}
