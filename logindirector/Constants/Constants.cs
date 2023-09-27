namespace logindirector.Constants
{
    /**
     * Class to hold all constants for the application
     */
    public class AppConstants
    {
        public static string Adaptor_OrgSchemeType = "GB-CCS";

        public static string RoleKey_JaeggerSupplier = "JAEGGER_SUPPLIER";
        public static string RoleKey_JaeggerBuyer = "JAEGGER_BUYER";
        public static string RoleKey_CatUser = "CAT_USER";
        public static string RoleKey_Evaluator = "EVALUATOR"; // TODO: Need to set the real value here from PPG

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

        public static string Tenders_UserCreation_Success = "UserCreated";
        public static string Tenders_UserCreation_MissingRole = "RoleMissing";
        public static string Tenders_UserCreation_Conflict = "RoleMismatch";
        public static string Tenders_UserCreation_HelpdeskRequired = "HelpdeskNeeded";
        public static string Tenders_UserCreation_Error = "UnexpectedFailure";
        public static string Tenders_UserCreation_AlreadyExists = "UserAlreadyExists";

        public static string Tenders_PostProcessingStatus_Valid = "ValidState";
        public static string Tenders_PostProcessingStatus_MergeFailure = "MergeFailed";
        public static string Tenders_PostProcessingStatus_Conflict = "RoleConflict";
        public static string Tenders_PostProcessingStatus_WrongAccountMerged = "WrongMerge";
        public static string Tenders_PostProcessingStatus_EvaluatorMerged = "EvaluatorMerged";
        public static string Tenders_PostProcessingStatus_WrongType = "WrongAccountTypeMerged";
        public static string Tenders_PostProcessingStatus_RoleMismatch = "RoleMismatch";
        public static string Tenders_PostProcessingStatus_NotEnoughAccounts = "NotEnoughAccountsMerged";
        public static string Tenders_PostProcessingStatus_Error = "UnexpectedResponse";

        public static string Display_JaeggerServiceName = "CCS eSourcing";
        public static string Display_CatServiceName = "Contract Award Service";

        public static string ExistingRoleKey_Buyer = "buyer";
        public static string ExistingRoleKey_Supplier = "supplier";
        public static string ExistingRoleKey_Evaluator = "evaluator";

        public static string RoleSetup_EsourcingBothRoles = "BuyerAndSupplier";
        public static string RoleSetup_EsourcingBuyerOnly = "Buyer";
        public static string RoleSetup_EsourcingSupplierOnly = "Supplier";
        public static string RoleSetup_CasRole = "CAS";
        public static string RoleSetup_NoRoles = "InvalidRoleSetup";
    }
}
