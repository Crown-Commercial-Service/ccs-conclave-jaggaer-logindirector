using System;
using System.Threading.Tasks;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    public interface IAdaptorClientServices
    {
        Task<AdaptorUserModel> GetUserInformation(string username);

        Task<string> PerformAdaptorRequest(string routeUri);
    }
}
