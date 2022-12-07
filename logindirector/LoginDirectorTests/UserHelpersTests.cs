using logindirector.Constants;
using logindirector.Helpers;
using logindirector.Models.AdaptorService;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace LoginDirectorTests
{
    [TestClass]
    public class UserHelpersTests
    {
        internal UserHelpers userHelpers;
        internal AdaptorUserModel userModel;
        internal string fakeDomain = "fake-domain";

        [TestInitialize]
        public void Startup()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddMemoryCache();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IMemoryCache memoryCache = serviceProvider.GetService<IMemoryCache>();

            IConfigurationRoot configuration = new ConfigurationBuilder().Build();
            userHelpers = new UserHelpers(configuration, memoryCache);
            userModel = new AdaptorUserModel
            {
                coreRoles = new List<AdaptorUserRoleModel>(),
                additionalRoles = new List<string>()
            };
        }

        [TestMethod]
        public void User_With_Valid_Core_Role_Should_Return_True()
        {
            // Setup a valid core role for our fake AdaptorUserModel
            AdaptorUserRoleModel customRoleModel = new AdaptorUserRoleModel
            {
                serviceClientName = AppConstants.Adaptor_ClientRoleAssignment
            };
            userModel.coreRoles.Add(customRoleModel);

            // Now test our fake object against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, fakeDomain), true);
        }

        [TestMethod]
        public void User_With_Valid_Additional_Role_Should_Return_True()
        {
            // Setup a valid additional role for our fake AdaptorUserModel
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);

            // Now test our fake object against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, fakeDomain), true);
        }

        [TestMethod]
        public void User_Without_Valid_Role_Should_Return_False()
        {
            // Test our fake object against this methiod with no changes
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, fakeDomain), false);
        }
    }
}