using logindirector.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace logindirector.Controllers
{
    // Controller to hold all session related functionality - primarily related to the Backchannel Logout system
    public class SessionController : Controller
    {
        public IConfiguration _configuration { get; }

        public SessionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult BackchannelRpIframe()
        {
            BackchannelModel model = new BackchannelModel
            {
                ClientId = _configuration.GetValue<string>("SsoService:ClientId"),
                RedirectUrl = Request.Host.Host + "/director/process-user",
                SecurityApiUrl = _configuration.GetValue<string>("SsoService:SsoDomain"),
                SessionState = "" // TODO: Populate
            };

            return PartialView("~/Views/Backchannel/RpIframe.cshtml", model);
        }

        // TODO: Expose route to accept backchannel requests
    }
}
