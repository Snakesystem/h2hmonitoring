namespace H2HAPICore.Model.S21BO
{
    public class GenericResult
    {
        public bool result { get; set; }
        public string message { get; set; }
    }

    public class BankInstruction
    {
        public int BankInstructionNID { get; set; }

        public DateTime Date { get; set; }

        public string ClientID { get; set; }

        public string Type { get; set; }

        public string SourceBankAccountID { get; set; }

        public string SourceSavingsID { get; set; }

        public string SourceSavingsName { get; set; }

        public string TargetBankAccountID { get; set; }

        public string TargetSavingsID { get; set; }

        public string TargetSavingsName { get; set; }

        public decimal Amount { get; set; }

        public int BatchID { get; set; }

        public string BankInstructionType { get; set; }

        public string BankReference { get; set; }

        public bool Manual { get; set; }

        public bool Generated { get; set; }

        public bool Reconciled { get; set; }

        public bool Success { get; set; }

        public bool Checked { get; set; }

        public bool Approved { get; set; }

        public bool Rejected { get; set; }

        public bool Revised { get; set; }

        public int ChangeNID { get; set; }
    }

    public class BankInstructionResult
    {
        public DateTime Date { get; set; }

        public string Bank { get; set; }

        public string Action { get; set; }

        public int BatchID { get; set; }

        public int AutoID { get; set; }

        public string FileName { get; set; }

        public string ResultText { get; set; }

        public DateTime? LastUpdate { get; set; }

        public int? UserNID { get; set; }

        public string SysUserID { get; set; }

        public string TerminalID { get; set; }

    }
}
