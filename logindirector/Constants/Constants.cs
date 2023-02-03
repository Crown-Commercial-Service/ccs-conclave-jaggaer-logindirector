namespace logindirector.Constants
{
    // Class to hold all constants for the application
    public class AppConstants
    {
        public static string Adaptor_OrgSchemeType = "GB-CCS";
        public static string Adaptor_ClientRoleAssignment = "Login Director";

        public static string RoleKey_JaeggerSupplier = "JAEGGER_SUPPLIER";
        public static string RoleKey_JaeggerBuyer = "JAEGGER_BUYER";
        public static string RoleKey_CatUser = "CAT_USER";

        public static string Session_UserKey = "UserModel";
        public static string Session_RequestDetailsKey = "RequestDetails";
        public static string Session_ProcessingRequiredKey = "UserProcessingRequired";
        public static string Session_UserPreAuthenticated = "UserPreAuth";

        public static string CentralCache_Key = "UserSessions";

        public static string Tenders_UserStatus_ActionRequired = "UserPromptRequired";
        public static string Tenders_UserStatus_AlreadyMerged = "UserMerged";
        public static string Tenders_UserStatus_Error = "UnexpectedResponse";
        public static string Tenders_UserStatus_Unauthorised = "Unauthorised";
        public static string Tenders_UserStatus_Conflict = "RoleMismatch";
        public static string Tenders_UserStatus_MergeFailed = "UserMergeFailed";

        public static string Tenders_UserCreation_Success = "UserCreated";
        public static string Tenders_UserCreation_MissingRole = "RoleMissing";
        public static string Tenders_UserCreation_Conflict = "RoleMismatch";
        public static string Tenders_UserCreation_HelpdeskRequired = "HelpdeskNeeded";
        public static string Tenders_UserCreation_Error = "UnexpectedFailure";
        public static string Tenders_UserCreation_AlreadyExists = "UserAlreadyExists";

        public static string Display_JaeggerServiceName = "CCS eSourcing";
        public static string Display_CatServiceName = "Contract Award Service";

        public static string Tenders_Roles_Buyer = "buyer";
        public static string Tenders_Roles_Supplier = "supplier";
    }
}
