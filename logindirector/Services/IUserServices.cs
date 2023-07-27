using System;
using System.Threading.Tasks;
using logindirector.Models;
using logindirector.Models.AdaptorService;

namespace logindirector.Services
{
    /**
     * Interface class for UserServices
     */
    public interface IUserServices
	{
        bool DoesUserHaveValidRolePreProcessing(AdaptorUserModel userModel, RequestSessionModel requestSessionModel);

        Task<string> GetEsourcingSsoRoleState(string username);

        Task<string> GetCasSsoRoleState(string username);
    }
}

