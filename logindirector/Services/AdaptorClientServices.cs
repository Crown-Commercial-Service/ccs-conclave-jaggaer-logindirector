using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Rollbar;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    // Service Client for the SSO Adaptor service - where we fetch user and department data from
    public class AdaptorClientServices : IAdaptorClientServices
    {
        public IConfiguration Configuration { get; }

        public AdaptorClientServices(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // Retrieves the user information associated with the specified username from the adaptor service
        public async Task<AdaptorUserModel> GetUserInformation(string username)
        {
            AdaptorUserModel userInfo = new AdaptorUserModel();

            try
            {
                // Fetch the information we need from the User Information route
                string userInfoRouteUri = Configuration.GetValue<string>("SsoService:SsoDomain") + Configuration.GetValue<string>("SsoService:RoutePaths:AdaptorPath");

                string responseContent = await PerformAdaptorRequest(userInfoRouteUri + "?user-name=" + HttpUtility.HtmlEncode(username));

                if (responseContent != null)
                {
                    // We've got a response, so map the content to our object
                    userInfo = JsonConvert.DeserializeObject<AdaptorUserModel>(responseContent);
                }
            }
            catch (Exception ex)
            {
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return userInfo;
        }

        // Core method that performs a request to the adaptor service using parameters passed to it
        public async Task<string> PerformAdaptorRequest(string routeUri)
        {
            string responseContent = "";

            try
            {
                string adaptorKey = Configuration.GetValue<string>("SsoService:AdaptorKey"),
                       clientKey = Configuration.GetValue<string>("SsoService:ClientId");

                // Establish a GET request to the specified route
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, routeUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("X-API-Key", adaptorKey);
                request.Headers.Add("X-Consumer-ClientId", clientKey);

                HttpClientHandler handler = new HttpClientHandler();
                using (HttpClient client = new HttpClient(handler))
                {
                    HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    responseContent = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                RollbarLocator.RollbarInstance.Error(ex);
            }

            return responseContent;
        }
    }
}
