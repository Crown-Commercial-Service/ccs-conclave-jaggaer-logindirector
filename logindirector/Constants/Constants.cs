﻿namespace logindirector.Constants
{
    // Class to hold all constants for the application
    public class AppConstants
    {
        public static string Adaptor_OrgSchemeType = "GB-CCS";
        public static string Adaptor_ClientRoleAssignment = "Login Director";

        public static string RoleKey_JaeggerSupplier = "JAEGGER_SUPPLIER";
        public static string RoleKey_JaeggerBuyer = "JAEGGER_BUYER";
        public static string RoleKey_CatUser = "CAT_USER_LOGIN_DIRECTOR";

        public static string Session_UserKey = "UserModel";

        public static string CentralCache_Key = "UserSessions";

        public static string Tenders_UserStatus_ActionRequired = "UserPromptRequired";
        public static string Tenders_UserStatus_AlreadyMerged = "UserMerged";
        public static string Tenders_UserStatus_Error = "UnexpectedResponse";
    }
}