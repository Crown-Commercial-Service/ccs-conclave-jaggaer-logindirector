namespace logindirector.Constants
{
    // Class to hold all constants for the application
    public class AppConstants
    {
        public const string Adaptor_OrgSchemeType = "GB-CCS";

        public const string RoleKey_JaeggerSupplier = "JAEGGER_SUPPLIER";
        public const string RoleKey_JaeggerBuyer = "JAEGGER_BUYER";
        public const string RoleKey_CatUser = "CAT_USER";

        public const string Session_UserKey = "UserModel";
        public const string Session_RequestDetailsKey = "RequestDetails";
        public const string Session_ProcessingRequiredKey = "UserProcessingRequired";
        public const string Session_UserPreAuthenticated = "UserPreAuth";

        public const string CentralCache_Key = "UserSessions";

        public const string Tenders_UserStatus_ActionRequired = "UserPromptRequired";
        public const string Tenders_UserStatus_AlreadyMerged = "UserMerged";
        public const string Tenders_UserStatus_Error = "UnexpectedResponse";
        public const string Tenders_UserStatus_Unauthorised = "Unauthorised";
        public const string Tenders_UserStatus_Conflict = "RoleMismatch";

        public const string Tenders_UserCreation_Success = "UserCreated";
        public const string Tenders_UserCreation_MissingRole = "RoleMissing";
        public const string Tenders_UserCreation_Conflict = "RoleMismatch";
        public const string Tenders_UserCreation_HelpdeskRequired = "HelpdeskNeeded";
        public const string Tenders_UserCreation_Error = "UnexpectedFailure";
        public const string Tenders_UserCreation_AlreadyExists = "UserAlreadyExists";

        public const string Display_JaeggerServiceName = "CCS eSourcing";
        public const string Display_CatServiceName = "Contract Award Service";
    }
}