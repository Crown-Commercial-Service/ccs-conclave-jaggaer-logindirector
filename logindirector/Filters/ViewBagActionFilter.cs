using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace logindirector.Filters
{
    // Action Filter to allow us to insert ViewBag values into all views by default, without needing to do it manually each time
    public class ViewBagActionFilter : ActionFilterAttribute
    {
        public IConfiguration _configuration { get; }

        public ViewBagActionFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override void OnResultExecuting(ResultExecutingContext context)
        {
            // Add any values to the ViewBag that we'll need globally
            if (context.Controller is Controller)
            {
                Controller controller = context.Controller as Controller;
                string requestSource = "https://" + context.HttpContext.Request.Host.Host + context.HttpContext.Request.Path;

                string opIframeUrl = _configuration.GetValue<string>("SsoService:SsoDomain") + _configuration.GetValue<string>("SsoService:RoutePaths:BackchannelPath") + requestSource;
                controller.ViewData.Add("OpIframeUrl", opIframeUrl);
            }

            base.OnResultExecuting(context);
        }
    }
}
