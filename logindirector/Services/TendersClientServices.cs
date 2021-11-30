using System;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace logindirector.Services
{
    // Service Client for the Tenders API service - where Jaegger operations are performed against
    public class TendersClientServices : ITendersClientServices
    {
        private readonly ILogger<TendersClientServices> _logger;
        public IConfiguration Configuration { get; }

        public TendersClientServices(ILogger<TendersClientServices> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
        }

        // Retrieves the status of a Jaegger user matching the authenticated user
        // TODO: Change output format to model once route works correctly
        public async Task<string> GetUserStatus(string username)
        {
            string tempReturnValue = "";

            try
            {
                // Fetch the information we need from the User route
                string userRouteUri = Configuration.GetValue<string>("TendersApi:ApiDomain") + Configuration.GetValue<string>("TendersApi:RoutePaths:UserPath");

                string responseContent = await PerformTendersRequest(userRouteUri + HttpUtility.HtmlEncode(username));

                if (responseContent != null)
                {
                    // TODO: Map response to a model, including status code (can't do this until the result is as we expect though).  For now, just return the string
                    tempReturnValue = responseContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Could not retrieve user information from Tenders API for " + username, ex);
            }

            return tempReturnValue;
        }

        // Core method that performs a request to the Tenders API using parameters passed to it
        public async Task<string> PerformTendersRequest(string routeUri)
        {
            string responseContent = "";

            try
            {
                // TODO: Communicate with API.  Need to see the Postman collection to see what I need to pass before I can write this
            }
            catch (Exception ex)
            {
                _logger.LogError("Error communicating with Tenders API at " + routeUri, ex);
            }

            return responseContent;
        }
    }
}
