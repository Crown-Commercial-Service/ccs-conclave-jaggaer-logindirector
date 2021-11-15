using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using logindirector.Constants;
using logindirector.Models;

// Controller to handle all incoming and outgoing requests to and from the application
namespace logindirector.Controllers
{
    public class RequestController : Controller
    {
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
            // TODO: check to see if user has active session in central cache
            // If yes, process the request
            // If no, destroy their session and authentication and redirect to process user route

            // TODO (in other case): Delete view and do work here instead
            return View("~/Views/Home/ProcessRequest.cshtml");
        }

        // TODO: Change this to a proper error setup later
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
