using System;
using System.Collections.Generic;
using System.Net;
using logindirector.Constants;
using logindirector.Models.AdaptorService;
using logindirector.Models.TendersApi;
using logindirector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LoginDirectorTests
{
	[TestClass]
	public class UserStatusTests
	{
		internal TendersClientServices tendersClientServices;
        string tendersBuyerResponseJson = "{\"roles\":[\"buyer\"]}";
        string tendersSupplierResponseJson = "{\"roles\":[\"supplier\"]}";

        [TestInitialize]
		public void Startup()
		{
            IConfigurationRoot configuration = new ConfigurationBuilder().Build();
			tendersClientServices = new TendersClientServices(configuration);
        }

		[TestMethod]
		public void Preprocessing_New_User_Should_Return_Action_Required()
		{
			// Set up a new user response from our mock request
			GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
			responseModel.StatusCode = HttpStatusCode.NotFound;

            // Now test our fake object against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_ActionRequired);
        }

		[TestMethod]
		public void Preprocessing_Forbidden_Should_Return_Unauthorised()
		{
            // Set up a new user response from our mock request
            GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
			responseModel.StatusCode = HttpStatusCode.Forbidden;

            // Now test our fake object against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Unauthorised);
        }

		[TestMethod]
		public void Preprocessing_Conflict_Should_Return_Conflict()
		{
            // Set up a new user response from our mock request
            GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
            responseModel.StatusCode = HttpStatusCode.Conflict;

            // Now test our fake object against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Conflict);
        }

		[TestMethod]
		public void Preprocessing_OK_Should_Return_Already_Merged()
		{
            // Set up a new user response from our mock request
            GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
            responseModel.StatusCode = HttpStatusCode.OK;

            // Now test our fake object against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_AlreadyMerged);
        }

		[TestMethod]
		public void Preprocessing_New_User_Without_Response_Value_Should_Return_Error()
		{
            // Set up a new user response from our mock request
            GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
            responseModel.StatusCode = HttpStatusCode.NotFound;
			responseModel.ResponseValue = "";

            // Now test our fake object against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Error);
        }

		[TestMethod]
		public void Preprocessing_Other_States_Should_Return_Error()
		{
            // Set up a couple of mock responses to test against
            GenericResponseModel responseModel = GetGenericResponseModelForPreproccessingTests();
            responseModel.StatusCode = HttpStatusCode.NotAcceptable;

            GenericResponseModel secondResponseModel = GetGenericResponseModelForPreproccessingTests();
            secondResponseModel.StatusCode = HttpStatusCode.InternalServerError;

            GenericResponseModel finalResponseModel = GetGenericResponseModelForPreproccessingTests();
            finalResponseModel.StatusCode = HttpStatusCode.Continue;

            // Now test our fake objects against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePreProcessing(responseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Error);

            model = tendersClientServices.HandleUserStatusResponsePreProcessing(secondResponseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Error);

            model = tendersClientServices.HandleUserStatusResponsePreProcessing(finalResponseModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Error);
        }

		internal GenericResponseModel GetGenericResponseModelForPreproccessingTests()
		{
			// We'll have to set the status code individually per test, but set the default response value to always return (it isn't always expected by what we're testing, but shouldn't interfere with the results)
			GenericResponseModel model = new GenericResponseModel
			{
				ResponseValue = "test@testmail.com not found in Jaggaer"
			};

			return model;
        }

        [TestMethod]
        public void Postprocessing_PPG_Buyer_With_Jaegger_Buyer_Response_Should_Return_Already_Merged()
        {
            // Set up the mock objects we need to test against
            GenericResponseModel responseModel = new GenericResponseModel
            {
                StatusCode = HttpStatusCode.OK,
                ResponseValue = tendersBuyerResponseJson
            };
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                additionalRoles = new List<string>()
            };
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);

            // Now test our fake objects against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePostProcessing(responseModel, userModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_AlreadyMerged);
        }

        [TestMethod]
        public void Postprocessing_PPG_Buyer_Without_Jaegger_Buyer_Response_Should_Return_Merge_Failure()
        {
            // Set up the mock objects we need to test against
            GenericResponseModel responseModel = new GenericResponseModel
            {
                StatusCode = HttpStatusCode.OK,
                ResponseValue = "{\"roles\":[\"\"]}"
            };
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                additionalRoles = new List<string>()
            };
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);

            // Now test our fake objects against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePostProcessing(responseModel, userModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_MergeFailed);
        }

        [TestMethod]
        public void Postprocessing_PPG_Buyer_With_Jaegger_Supplier_Response_Should_Return_Conflict()
        {
            // Set up the mock objects we need to test against
            GenericResponseModel responseModel = new GenericResponseModel
            {
                StatusCode = HttpStatusCode.OK,
                ResponseValue = tendersSupplierResponseJson
            };
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                additionalRoles = new List<string>()
            };
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerBuyer);

            // Now test our fake objects against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePostProcessing(responseModel, userModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Conflict);
        }

        [TestMethod]
        public void Postprocessing_PPG_Supplier_With_Jaegger_Buyer_Response_Should_Return_Conflict()
        {
            // Set up the mock objects we need to test against
            GenericResponseModel responseModel = new GenericResponseModel
            {
                StatusCode = HttpStatusCode.OK,
                ResponseValue = tendersBuyerResponseJson
            };
            AdaptorUserModel userModel = new AdaptorUserModel
            {
                additionalRoles = new List<string>()
            };
            userModel.additionalRoles.Add(AppConstants.RoleKey_JaeggerSupplier);

            // Now test our fake objects against the method
            UserStatusModel model = tendersClientServices.HandleUserStatusResponsePostProcessing(responseModel, userModel);
            Assert.IsTrue(model.UserStatus == AppConstants.Tenders_UserStatus_Conflict);
        }
    }
}

