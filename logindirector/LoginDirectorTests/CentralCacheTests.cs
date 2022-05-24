using System;
using System.Collections.Generic;
using System.Security.Claims;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Helpers;
using logindirector.Models;
using logindirector.Models.AdaptorService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoginDirectorTests
{
    [TestClass]
    public class CentralCacheTests
    {
        internal RequestController requestController;
        internal UserHelpers userHelpers;
        internal UserProcessingController userProcessingController;
        string commonTestEmail = "test@testmail.com";

        [TestInitialize]
        public void Startup()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddMemoryCache();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IMemoryCache memoryCache = serviceProvider.GetService<IMemoryCache>();

            IConfigurationRoot configuration = new ConfigurationBuilder().Build();
            userHelpers = new UserHelpers(configuration, memoryCache);

            requestController = new RequestController(memoryCache, configuration, userHelpers);
            userProcessingController = new UserProcessingController(null, null, null, memoryCache, configuration);
        }

        [TestMethod]
        public void User_With_No_User_Object_Should_Return_False()
        {
            // By default in these tests there will be no User object in session.  So just run the test directly
            Assert.AreEqual(userHelpers.DoesUserHaveValidSession(new DefaultHttpContext(), commonTestEmail).Result, false);
        }

        [TestMethod]
        public void User_With_Valid_Matching_Entry_In_Cache_Should_Return_True()
        {
            // Create an User object which the test can use
            Setup_Test_ClaimsPrincipal(commonTestEmail);

            // Now add an entry into the central cache which started 2 mins ago and should match
            Setup_Test_Cache_Entry(-2, commonTestEmail);

            // Finally, run the test
            Assert.AreEqual(userHelpers.DoesUserHaveValidSession(requestController.ControllerContext.HttpContext, commonTestEmail).Result, true);
        }

        [TestMethod]
        public void User_With_Expired_Matching_Entry_In_Cache_Should_Return_False()
        {
            // Create an User object which the test can use
            Setup_Test_ClaimsPrincipal(commonTestEmail);

            // Now add an entry into the central cache which started 45 mins ago and should match
            Setup_Test_Cache_Entry(-45, commonTestEmail);

            // Finally, run the test
            Assert.AreEqual(userHelpers.DoesUserHaveValidSession(requestController.ControllerContext.HttpContext, commonTestEmail).Result, false);
        }

        [TestMethod]
        public void User_Without_Match_In_Cache_Should_Return_False()
        {
            // Create an User object which the test can use
            Setup_Test_ClaimsPrincipal(commonTestEmail);

            // Now add an entry into the central cache which started 2 mins ago but does NOT match
            Setup_Test_Cache_Entry(-2, "testing@testmail.com");

            // Finally, run the test
            Assert.AreEqual(userHelpers.DoesUserHaveValidSession(requestController.ControllerContext.HttpContext, commonTestEmail).Result, false);
        }

        [TestMethod]
        public void Addition_To_Cache_Request_For_Null_User_Should_Return_False()
        {
            // Run the test with a null user object
            userProcessingController.AddUserToCentralSessionCache(null);

            List<UserSessionModel> sessionsList = new List<UserSessionModel>();
            string cacheKey = AppConstants.CentralCache_Key;

            if (userProcessingController._memoryCache.TryGetValue(cacheKey, out sessionsList))
            {
                Assert.IsTrue(sessionsList.Count == 0);
            }

        }

        [TestMethod]
        public void Addition_To_Cache_Request_For_User_Without_Email_Should_Return_False()
        {
            // Run the test with a user object that has no email address assigned
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                emailAddress = ""
            };
            userProcessingController.AddUserToCentralSessionCache(userModel);

            List<UserSessionModel> sessionsList = new List<UserSessionModel>();
            string cacheKey = AppConstants.CentralCache_Key;

            if (userProcessingController._memoryCache.TryGetValue(cacheKey, out sessionsList))
            {
                Assert.IsTrue(sessionsList.Count == 0);
            }
        }

        [TestMethod]
        public void Addition_To_Cache_Request_For_Valid_User_Should_Return_True()
        {
            // Run the test with a user object that has an email address assigned
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                emailAddress = commonTestEmail
            };
            userProcessingController.AddUserToCentralSessionCache(userModel);

            List<UserSessionModel> sessionsList = new List<UserSessionModel>();
            string cacheKey = AppConstants.CentralCache_Key;

            if (userProcessingController._memoryCache.TryGetValue(cacheKey, out sessionsList))
            {
                Assert.IsTrue(sessionsList.Count == 1);
            }
        }

        internal void Setup_Test_ClaimsPrincipal(string emailAddress)
        {
            List<Claim> claims = new List<Claim>()
            {
                new Claim(ClaimTypes.Email, emailAddress)
            };
            ClaimsIdentity identity = new ClaimsIdentity(claims, "TestAuthType");
            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(identity);
            requestController.ControllerContext = new ControllerContext();
            requestController.ControllerContext.HttpContext = new DefaultHttpContext { User = claimsPrincipal };
        }

        internal void Setup_Test_Cache_Entry(int sessionAge, string emailAddress)
        {
            UserSessionModel userEntry = new UserSessionModel
            {
                userEmail = emailAddress,
                sessionStart = DateTime.Now.AddMinutes(sessionAge),
                sessionId = "123456789"
            };

            List<UserSessionModel> sessionsList = new List<UserSessionModel>
            {
                userEntry
            };

            requestController._memoryCache.Set(AppConstants.CentralCache_Key, sessionsList);
        }
    }
}
