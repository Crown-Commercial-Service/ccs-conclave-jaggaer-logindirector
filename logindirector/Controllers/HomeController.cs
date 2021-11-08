using System.Diagnostics;
using System.Linq;
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using logindirector.Services;
using logindirector.Constants;
using logindirector.Helpers;

namespace logindirector.Controllers
{
    // TODO: Remove this controller when we deal with the story to handle incoming requests.  These are currently just temporary pages to allow for local dev testing
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public IAdaptorClientServices _adaptorClientServices;
        public IHelpers _userHelpers;

        public HomeController(ILogger<HomeController> logger, IAdaptorClientServices adaptorClientServices, IHelpers userHelpers)
        {
            _logger = logger;
            _adaptorClientServices = adaptorClientServices;
            _userHelpers = userHelpers;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            string test = HttpContext.Session.GetString(AppConstants.Session_UserKey);

            return View();
        }

        [Authorize]
        public IActionResult AuthTest()
        {
            // Check if we have a user object already in session - if we don't, we need to build it via the adaptor service
            if (String.IsNullOrWhiteSpace(HttpContext.Session.GetString(AppConstants.Session_UserKey)) && !String.IsNullOrWhiteSpace(User?.Claims?.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value))
            {
                // User appears to be successfully authenticated with SSO service - so fetch their user data from the adaptor service
                AdaptorUserModel userModel = _adaptorClientServices.GetUserInformation(User.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email).Value).Result;

                if (userModel != null && _userHelpers.HasValidUserRoles(userModel))
                {
                    // Serialise the model as JSON and store it in the session - we'll need to deserialise it again to use it later
                    HttpContext.Session.SetString(AppConstants.Session_UserKey, JsonConvert.SerializeObject(userModel));
                }
                else
                {
                    // TODO: When we refactor this into the real route, we need to do error handling here to allow for it to detect and route to unauthorised display
                    _logger.LogError("Attempted access by unauthorised SSO user");
                }
            }

            return View();
        }

        public IActionResult Unauthorised()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
