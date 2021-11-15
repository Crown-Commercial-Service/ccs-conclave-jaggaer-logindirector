using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using logindirector.Models.AdaptorService;
using logindirector.Services;
using logindirector.Helpers;
using logindirector.Constants;

// Controller to handle all user processing actions done by the application, before outgoing requests are applied
namespace logindirector.Controllers
{
    public class UserProcessingController : Controller
    {
        private readonly ILogger<UserProcessingController> _logger;
        public IAdaptorClientServices _adaptorClientServices;
        public IHelpers _userHelpers;

        public UserProcessingController(ILogger<UserProcessingController> logger, IAdaptorClientServices adaptorClientServices, IHelpers userHelpers)
        {
            _logger = logger;
            _adaptorClientServices = adaptorClientServices;
            _userHelpers = userHelpers;
        }

        // Route to process all users logging into the system - account interactions in Jaegger / CaT, and store the data we need for later
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
                    // TODO: Central cache goes here

                    // TODO: Tenders API interaction will go here

                    // We've done all we need to here, so now send the user to have their initial request processed
                    return RedirectToAction("ActionRequest", "Request");
                }
                else
                {
                    // User is not permitted to use the Login Director - log error, and present error
                    _logger.LogError("Attempted access by unauthorised SSO user - " + userEmail);

                    // TODO: Change this to a dedicated error page display
                    return View("~/Views/Errors/Generic.cshtml");
                }
            }

            // If we've got to here, the user isn't properly authenticated.  Display an error
            // TODO: Change this to a dedicated error page display
            return View("~/Views/Errors/Generic.cshtml");
        }
    }
}
