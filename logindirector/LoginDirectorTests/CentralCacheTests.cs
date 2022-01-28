using System;
using System.Collections.Generic;
using System.Security.Claims;
using logindirector.Constants;
using logindirector.Controllers;
using logindirector.Models;
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

        [TestInitialize]
        public void Startup()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddMemoryCache();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IMemoryCache memoryCache = serviceProvider.GetService<IMemoryCache>();

            IConfigurationRoot configuration = new ConfigurationBuilder().Build();

            requestController = new RequestController(memoryCache, configuration);
        }

        [TestMethod]
        public void User_With_No_User_Object_Should_Return_False()
        {
            // By default in these tests there will be no User object in session.  So just run the test directly
            Assert.AreEqual(requestController.DoesUserHaveValidSession(), false);
        }

        [TestMethod]
        public void User_With_Valid_Matching_Entry_In_Cache_Should_Return_True()
        {
            // Create an User object which the test can use
            string testEmail = "test@testmail.com";
            Setup_Test_ClaimsPrincipal(testEmail);

            // Now add an entry into the central cache which started 2 mins ago and should match
            Setup_Test_Cache_Entry(-2, testEmail);

            // Finally, run the test
            Assert.AreEqual(requestController.DoesUserHaveValidSession(), true);
        }

        [TestMethod]
        public void User_With_Expired_Matching_Entry_In_Cache_Should_Return_False()
        {
            // Create an User object which the test can use
            string testEmail = "test@testmail.com";
            Setup_Test_ClaimsPrincipal(testEmail);

            // Now add an entry into the central cache which started 45 mins ago and should match
            Setup_Test_Cache_Entry(-45, testEmail);

            // Finally, run the test
            Assert.AreEqual(requestController.DoesUserHaveValidSession(), false);
        }

        [TestMethod]
        public void User_Without_Match_In_Cache_Should_Return_False()
        {
            // Create an User object which the test can use
            Setup_Test_ClaimsPrincipal("test@testmail.com");

            // Now add an entry into the central cache which started 2 mins ago but does NOT match
            Setup_Test_Cache_Entry(-2, "testing@testmail.com");

            // Finally, run the test
            Assert.AreEqual(requestController.DoesUserHaveValidSession(), false);
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
                sessionStart = DateTime.Now.AddMinutes(sessionAge)
            };

            List<UserSessionModel> sessionsList = new List<UserSessionModel>
            {
                userEntry
            };

            requestController._memoryCache.Set(AppConstants.CentralCache_Key, sessionsList);
        }

        // if null user is requested, nothing should be added
        // if valid user is requested, entry should be added that matches
    }
}
