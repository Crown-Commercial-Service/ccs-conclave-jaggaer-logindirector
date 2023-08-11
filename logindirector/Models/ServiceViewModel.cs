namespace logindirector.Models
{
    public class ServiceViewModel
    {
        public string ServiceDisplayName { get; set; }

        public bool ShowBuyerError { get; set; }

        public bool ShowSupplierError { get; set; }

        public bool ShowProcessConflictError { get; set; }

        public bool ShowProcessEvaluatorError { get; set; }

        public bool ShowProcessTypeError { get; set; }

        public bool ShowProcessNotEnoughAccountsError { get; set; }
    }
}
