

using H2HAPICore.Model.BCA;
using System.Globalization;
using System.Runtime.Serialization;
using static H2HAPICore.Model.Permata.TokenRequest;

namespace H2HAPICore.Model.Permata
{
    public class Token
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string accessToken { get; set; }
        public string tokenType { get; set; }
        public string expiresIn { get; set; }
    }

    public class Settings
    {
        public bool DepositOnly { get; set; }
        public bool DisableTaxInterest { get; set; }
        public string URL { get; set; }
        public string PARTNERID { get; set; }
        public string clientid { get; set; }
        public string clientsecret { get; set; }
        public string authId { get; set; }
        public string apisecret { get; set; }

    }

    public class TokenRequest
    {
        public string grantType { get; set; }

        public AdditionalInfo additionalInfo { get; set; }

        public class AdditionalInfo
        {
            public string authId { get; set; }

            public string authSecret { get; set; }
        }

        public TokenRequest()
        {
            additionalInfo = new AdditionalInfo();
        }

    }

    public class InvestorAccountStatementResponse
    {
        public NotificationTransactionRs NotificationTransactionRs { get; set; }
        public InvestorAccountStatementResponse()
        {
            NotificationTransactionRs = new NotificationTransactionRs();
        }
    }

    public class MsgRsHdr
    {
        public string ResponseTimestamp { get; set; }
        public string CustRefID { get; set; }
        public string StatusCode { get; set; }
        public string StatusDesc { get; set; }
    }
    public class NotificationTransactionRs
    {
        public MsgRsHdr MsgRsHdr { get; set; }
        public NotificationTransactionRs()
        {
            MsgRsHdr = new MsgRsHdr();
        }
    }

    public class InvestorAccountStatementRequest
    {
        public NotificationTransactionRq NotificationTransactionRq { get; set; }
        public InvestorAccountStatementRequest()
        {
            NotificationTransactionRq = new NotificationTransactionRq();
        }
    }

    public class NotificationTransactionRq
    {
        public MsgRqHdr MsgRqHdr { get; set; }
        public TransactionInfo TransactionInfo { get; set; }
    }

    public class MsgRqHdr
    {
        public string RequestTimestamp { get; set; }
        public string CustRefID { get; set; }
    }

    public class PERMATA_AccountStatement
    {
        public long Statement_ID { get; set; }

        public string CashValue { get; set; }

        public string OpeningBalance { get; set; }

        public string CloseBal { get; set; }

        public string TxnDate { get; set; }

        public string ExtRef { get; set; }

        public string CustRefID { get; set; }

        public string GroupID { get; set; }

        public string SeqNum { get; set; }
        public string AccountNumber { get; set; }

        public string Currency { get; set; }
        public string ValueDate { get; set; }

        public string TrxType { get; set; }

        public string Flag { get; set; }
        public string DC { get; set; }


        public string Description { get; set; }

        public string Notes { get; set; }

        public int? Status { get; set; }
        public string StatusReason { get; set; }

        public int? FundInOutNID { get; set; }

        public int? FundNID { get; set; }

        public bool? inProc { get; set; }

        public string ClientID { get; set; }
        public string ClientNID { get; set; }

        public DateTime? ReceiveTime { get; set; }

        public PERMATA_AccountStatement() { }

        public PERMATA_AccountStatement(InvestorAccountStatementRequest req)
        {
            CashValue = req.NotificationTransactionRq.TransactionInfo.Statements.CashValue;
            OpeningBalance = req.NotificationTransactionRq.TransactionInfo.OpeningBalance;
            CloseBal = req.NotificationTransactionRq.TransactionInfo.CloseBal;
            TxnDate = req.NotificationTransactionRq.MsgRqHdr.RequestTimestamp;
            ExtRef = req.NotificationTransactionRq.TransactionInfo.Statements.ExtRef;
            SeqNum = req.NotificationTransactionRq.TransactionInfo.SeqNum;
            AccountNumber = req.NotificationTransactionRq.TransactionInfo.AccountNumber;
            Currency = req.NotificationTransactionRq.TransactionInfo.Currency;
            TrxType = req.NotificationTransactionRq.TransactionInfo.Statements.TrxType;
            CustRefID = req.NotificationTransactionRq.MsgRqHdr.CustRefID;
            GroupID = req.NotificationTransactionRq.TransactionInfo.GroupID;
            ValueDate = req.NotificationTransactionRq.TransactionInfo.ValueDate;
            Flag = req.NotificationTransactionRq.TransactionInfo.Statements.Flag;
            DC = req.NotificationTransactionRq.TransactionInfo.Statements.DC;
            Description = req.NotificationTransactionRq.TransactionInfo.Statements.Description;
            Notes = req.NotificationTransactionRq.TransactionInfo.Notes;
            inProc = false;
            ReceiveTime = DateTime.Now;
        }
    }

    public class TransactionInfo
    {
        public string GroupID { get; set; }
        public string SeqNum { get; set; }
        public string AccountNumber { get; set; }
        public string Currency { get; set; }
        public string ValueDate { get; set; }
        public string OpeningBalance { get; set; }
        public Statements Statements { get; set; }
        public string CloseBal { get; set; }
        public string Notes { get; set; }
    }

    public class Statements
    {
        public string ExtRef { get; set; }
        public string TrxType { get; set; }
        public string DC { get; set; }
        public string Flag { get; set; }
        public string CashValue { get; set; }
        public string Description { get; set; }
    }

    public class PermataOverBookRequest
    {
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryEmail { get; set; }
        public string currency { get; set; }
        public string customerReference { get; set; }
        public string remark { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalBookInfoPermata additionalInfo { get; set; }

        public PermataOverBookRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalBookInfoPermata();
        }
    }
    public class PermataOverBookResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string transactionStatus { get; set; }

    }
    public class AdditionalBookInfoPermata
    {
        public string beneficiaryAccountName { get; set; }
        public string sourceAccountName { get; set; }
    }
    public class Amount
    {
        public string value { get; set; }
        public string currency { get; set; }
    }
    public class PermataBIFASTRequest
    {

        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountName { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalBIFASTPermata additionalInfo { get; set; }

        public PermataBIFASTRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalBIFASTPermata();
        }
    }

    public class AdditionalBIFASTPermata
    {
        public string sourceAccountName { get; set; }
        public string beneficiaryBankName { get; set; }
        public string chargeBearerCode { get; set; }
        public string beneficiaryAccountType { get; set; }
        public string purposeOfTransaction { get; set; }
        public string beneficiaryCustomerIdNumber { get; set; }
        public string beneficiaryCustomerType { get; set; }
        public string beneficiaryCustomerResidentStatus { get; set; }
        public string beneficiaryCustomerTownName { get; set; }
        public string memo { get; set; }
    }

    public class PermataBIFASTResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string sourceAccountNo { get; set; }

    }

    public class PermataLLGRTGSRequest
    {

        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountName { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryAddress { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string beneficiaryBankName { get; set; }
        public string beneficiaryCustomerResidence { get; set; }
        public string beneficiaryCustomerType { get; set; }
        public string beneficiaryEmail { get; set; }
        public string currency { get; set; }
        public string feeType { get; set; }
        public string remark { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfoPermata additionalInfo { get; set; }

        public PermataLLGRTGSRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoPermata();
        }
    }

    public class AdditionalInfoPermata
    {
        public string beneficiaryCitizenStatus { get; set; }
        public string sourceAccountName { get; set; }
    }

    public class PermataLLGRTGSResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }
        public string beneficiaryAccountName { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string currency { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public string transactionStatus { get; set; }

    }
    public class PERMATA_OnlineTransfer
    {
        public long AutoNID { get; set; }
        public int BankInstructionNID { get; set; }
        public int BatchID { get; set; }
        public int ClientNID { get; set; }
        public string ClientID { get; set; }
        public string Amount { get; set; }
        public string SourceAccountNo { get; set; }
        public string SourceAccountName { get; set; }
        public string BeneficiaryAccountNo { get; set; }
        public string BeneficiaryAccountName { get; set; }
        public string Type { get; set; }
        public DateTime? SendTime { get; set; }
        public string TransactionDate { get; set; }
        public DateTime? ReceivedTime { get; set; }
        public string HTTPCode { get; set; }
        public string StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string ServiceCode { get; set; }
        public string BeneficiaryEmail { get; set; }
        public string BeneficiaryBankName { get; set; }
        public string BeneficiaryAccountType { get; set; }
        public string BeneficiaryCustomerIdNumber { get; set; }
        public string BeneficiaryCustomerType { get; set; }
        public string BeneficiaryCustomerResidentStatus { get; set; }
        public string BeneficiaryCustomerTownName { get; set; }
        public string BeneficiaryBankCode { get; set; }
        public string BeneficiaryAddress { get; set; }
        public string BeneficiaryCitizenStatus { get; set; }
        public string BeneficiaryCustomerResidence { get; set; }
        public string PartnerReferenceNo { get; set; }
        public string Remark { get; set; }
        public string Memo { get; set; }


    }
}
