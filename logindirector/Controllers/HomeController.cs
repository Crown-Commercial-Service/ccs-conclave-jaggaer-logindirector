using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using logindirector.Services;

namespace logindirector.Controllers
{
    // TODO: Remove this controller when we deal with the story to handle incoming requests.  These are currently just temporary pages to allow for local dev testing
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public IAdaptorClientServices _adaptorClientServices;

        public HomeController(ILogger<HomeController> logger, IAdaptorClientServices adaptorClientServices)
        {
            _logger = logger;
            _adaptorClientServices = adaptorClientServices;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult AuthTest()
        {
            //string test = User.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Role).Value;
            //string name = User.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email).Value;

            // TODO: Uuse above comments to check we have email in claims
            // Access adaptor service using claims
            // Register additional claims using values from adaptor service
            // Validate user access using roles
            // Refactor all of this into a separate method which can be called from many places if needed

            AdaptorUserModel tempModel = _adaptorClientServices.GetUserInformation("jaeggersup01@yopmail.com").Result;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
