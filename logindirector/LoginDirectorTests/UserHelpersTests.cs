using logindirector.Constants;
using logindirector.Helpers;
using logindirector.Models.AdaptorService;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using logindirector.Models;

namespace LoginDirectorTests
{
    [TestClass]
    public class UserHelpersTests
    {
        internal UserHelpers userHelpers;
        internal AdaptorUserModel userModel;
        internal RequestSessionModel requestSessionModel;

        internal string jaeggerTestDomain = "jaeggertest.com";
        internal string catTestDomain = "cattest.com";

        [TestInitialize]
        public void Startup()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddMemoryCache();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IMemoryCache memoryCache = serviceProvider.GetService<IMemoryCache>();

            Dictionary<string,string> testConfiguration = new Dictionary<string, string>
            {
                {"ExitDomains:CatDomain", catTestDomain},
                {"ExitDomains:JaeggerDomain", jaeggerTestDomain}
            };

            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(testConfiguration).Build();

            userHelpers = new UserHelpers(configuration, memoryCache);
            userModel = new AdaptorUserModel
            {
                coreRoles = new List<AdaptorUserRoleModel>(),
                additionalRoles = new List<string>()
            };
            requestSessionModel = new RequestSessionModel();
        }

        [TestMethod]
        public void CaT_User_On_CaT_Domain_Should_Return_True()
        {
            // Setup the CaT user role and CaT domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_CatUser);
            requestSessionModel.domain = catTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), true);
        }

        [TestMethod]
        public void Cat_User_On_Jaegger_Domain_Should_Return_False()
        {
            // Setup the CaT user role and Jaegger domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_CatUser);
            requestSessionModel.domain = jaeggerTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), false);
        }

        [TestMethod]
        public void Jaegger_Supplier_User_On_Jaegger_Domain_Should_Return_True()
        {
            // Setup the Jaegger Supplier user role and Jaegger domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerSupplier);
            requestSessionModel.domain = jaeggerTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), true);
        }

        [TestMethod]
        public void Jaegger_Buyer_User_On_Jaegger_Domain_Should_Return_True()
        {
            // Setup the Jaegger Buyer user role and Jaegger domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);
            requestSessionModel.domain = jaeggerTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), true);
        }

        [TestMethod]
        public void Jaegger_Supplier_User_On_CaT_Domain_Should_Return_False()
        {
            // Setup the Jaegger Supplier user role and CaT domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerSupplier);
            requestSessionModel.domain = catTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), false);
        }

        [TestMethod]
        public void Jaegger_Buyer_User_On_CaT_Domain_Should_Return_False()
        {
            // Setup the Jaegger Buyer user role and CaT domain for our fake models
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);
            requestSessionModel.domain = catTestDomain;

            // Now test our fake objects against the method
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), false);
        }

        [TestMethod]
        public void User_Without_Valid_Role_Should_Return_False()
        {
            // Test our fake object against this method with no roles added
            Assert.AreEqual(userHelpers.HasValidUserRoles(userModel, requestSessionModel), false);
        }
    }
}