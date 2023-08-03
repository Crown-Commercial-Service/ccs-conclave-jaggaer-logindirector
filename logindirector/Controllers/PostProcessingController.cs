using System;
using System.Linq;
using System.Runtime.CompilerServices;
using logindirector.Constants;
using logindirector.Helpers;
using logindirector.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using logindirector.Models.TendersApi;
using logindirector.Services;
using System.Linq.Expressions;

/**
 * Controller to handle users post-processing, when they're passed back to us to send onwards
 */
[assembly: InternalsVisibleTo("LoginDirectorTests")]
namespace logindirector.Controllers
{
	public class PostProcessingController : Controller
	{
        public IHelpers _userHelpers;
        public IConfiguration _configuration { get; }
        public ITendersClientServices _tendersClientServices;

        public PostProcessingController(IHelpers userHelpers, IConfiguration configuration, ITendersClientServices tendersClientServices)
        {
            _userHelpers = userHelpers;
            _configuration = configuration;
            _tendersClientServices = tendersClientServices;
        }

        /**
         * Route to process and execute outgoing requests, once a user has been logged in and processed
         */
        [Route("/director/action-request", Order = 1)]
        [Authorize]
        public async Task<IActionResult> ActionRequest()
        {
            // First, check to see that the user's session is valid in the central cache
            string userSid = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Sid)?.Value;

            if (await _userHelpers.DoesUserHaveValidSession(HttpContext, userSid))
            {
                // We also need to check to validate that processing has been successfully completed via another Tenders call
                string accessToken = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Authentication)?.Value,
                    userEmail = User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value,
                    requestDetails = HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey);

                if (!String.IsNullOrWhiteSpace(accessToken) && !String.IsNullOrWhiteSpace(userEmail) && !String.IsNullOrWhiteSpace(requestDetails))
                {
                    RequestSessionModel requestModel = JsonConvert.DeserializeObject<RequestSessionModel>(requestDetails);

                    if (requestModel != null)
                    {
                        UserStatusModel userStatusModel = await _tendersClientServices.GetUserStatusPostProcessing(userEmail, accessToken, requestModel.domain);

                        if (userStatusModel != null)
                        {
                            // Update our session value to indicate that the user has now been processed within this session
                            HttpContext.Session.SetString(AppConstants.Session_ProcessingRequiredKey, "false");

                            // Now check our status response to work out what to do with the user
                            if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_Valid)
                            {
                                // User appears to be valid, so now we can process their request from the stored request object as a GET redirect (POSTs were handled earlier)
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
                            else if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_MergeFailure)
                            {
                                // TODO: Display error page directing the user to CSC (MergeError.cshtml)
                            }
                            else if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_RoleMismatch)
                            {
                                // TODO: Display error page saying wrong account merged or permissions changed (new)
                            }
                            else
                            {
                                // This can be one of various conflict states - check to see which
                                if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_Conflict)
                                {
                                    // TODO: Display merge screen with an error saying “you’ve merged the wrong type of account” (e.g. buyer when supplier wanted)
                                }
                                else if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_EvaluatorMerged)
                                {
                                    // TODO: Display merge screen with an error saying “you’ve merged with an evaluator” (probably the same as Conflict above display wise)
                                }
                                else if (userStatusModel.UserStatus == AppConstants.Tenders_PostProcessingStatus_WrongType)
                                {
                                    // TODO: Display merge screen with an error saying “you’ve not merged what you need” (e.g. supplier merged by buyer wanted for CAS access - same as Conflict)
                                }
                                else
                                {
                                    // Has to be NotEnoughAccounts
                                    // TODO: Display merge screen with an error saying “you’ve not merged enough accounts” (e.g. buyer and supplier but only supplier - same as Conflict)
                                }
                            }
                        }

                        // If we've gotten this far there's been some kind of issue fetching the request details from session.  Display Session Expiry message
                        ErrorViewModel model = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                        return View("~/Views/Errors/SessionExpired.cshtml", model);
                    }
                }

                // There was an error fetching the user details from Tenders - display our Generic error view
                ErrorViewModel errorModel = _userHelpers.BuildErrorModelForUser(HttpContext.Session.GetString(AppConstants.Session_RequestDetailsKey));
                return View("~/Views/Errors/Generic.cshtml", errorModel);
            }
            else
            {
                // User does not appear to have a valid session in the central cache - clear them down and send them to re-authenticate at the Process User endpoint
                return RedirectToAction("ProcessUser", "UserProcessing");
            }
        }
    }
}

