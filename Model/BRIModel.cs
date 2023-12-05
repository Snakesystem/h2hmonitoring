

using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace H2HAPICore.Model.BRI
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
        public string URL { get; set; }
        public bool DepositOnly { get; set; }
        public bool DisableTaxInterest { get; set; }
        public string clientid { get; set; }
        public string clientsecret { get; set; }
    }

    public class TokenRequest
    {
        public string grantType { get; set; }

    }

    public class InvestorAccountStatementRequest
    {
        public string idTransaction { get; set; }
        public string externalReference { get; set; }
        public string seq { get; set; }
        public string accountNo { get; set; }
        public string accountCurrency { get; set; }
        public string transactionDate { get; set; }
        public string transactionCode { get; set; }
        public string transactionPosition { get; set; }
        public string amount { get; set; }
        public string openingBalance { get; set; }
        public string closingBalance { get; set; }
        public string transactionDescription { get; set; }
        public string SID { get; set; }
        public string SRE { get; set; }
        public string accountDebit { get; set; }
        public string accountCredit { get; set; }
    }

    public class InvestorAccountStatementResponse
    {
        public string responseCode { get; set; }
        public string responseDescription { get; set; }
        public StatementResponseData data { get; set; }

        public InvestorAccountStatementResponse()
        {
            data = new StatementResponseData();
        }
    }

    public class StatementResponseData
    {
        [DataMember]
        public string idTransaction { get; set; }
        [DataMember]
        public string externalReference { get; set; }
    }

    public class BRI_AccountStatement
    {
        public long Statement_ID { get; set; }

        public string Amount { get; set; }

        public string OpenBalance { get; set; }

        public string CloseBalance { get; set; }

        public string TransactionDate { get; set; }

        public string ExternalReference { get; set; }

        public string Seq { get; set; }

        public string AccountNo { get; set; }

        public string AccountCurrency { get; set; }

        public string TransactionPosition { get; set; }

        public string TransactionCode { get; set; }

        public string AccountDebit { get; set; }

        public string AccountCredit { get; set; }

        public string TransactionDescription { get; set; }

        public string IdTransaction { get; set; }

        public string SID { get; set; }

        public string SRE { get; set; }

        public int? Status { get; set; }
        public string StatusReason { get; set; }

        public int? FundInOutNID { get; set; }

        public int? FundNID { get; set; }

        public bool? inProc { get; set; }

        public string ClientID { get; set; }
        public string ClientNID { get; set; }

        public DateTime? ReceiveTime { get; set; }

        public BRI_AccountStatement() { }

        public BRI_AccountStatement(InvestorAccountStatementRequest req)
        {
            Amount = req.amount;
            OpenBalance = req.openingBalance;
            CloseBalance = req.closingBalance;
            TransactionDate = req.transactionDate;
            ExternalReference = req.externalReference;
            Seq = req.seq;
            AccountNo = req.accountNo;
            AccountCurrency = req.accountCurrency;
            TransactionPosition = req.transactionPosition;
            TransactionCode = req.transactionCode;
            AccountDebit = req.accountDebit;
            AccountCredit = req.accountCredit;
            TransactionDescription = req.transactionDescription;
            IdTransaction = req.idTransaction;
            SID = req.SID;
            SRE = req.SRE;
            inProc = false;
            ReceiveTime = DateTime.Now;
        }

    }

    public class InquiryBifastRequest
    {
        public string beneficiaryBankCode { get; set; }
        public string beneficiaryAccountNo { get; set; }

    }


    public class InquiryBifastResponse
    {
        public string responseCode { get; set; }

        public string responseMessage { get; set; }

        public string referenceNo { get; set; }

        public string externalId { get; set; }

        public string registrationId { get; set; }

        public string receiverName { get; set; }

        public string beneficiaryAccountNo { get; set; }

        public string beneficiaryBankCode { get; set; }

        public string beneficiaryAccountType { get; set; }

        public string accountNumber { get; set; }

        public string receiverType { get; set; }

        public string receiverResidentStatus { get; set; }

        public string receiverIdentityNumber { get; set; }

        public string receiverTownName { get; set; }

        public string currency { get; set; }
    }

    public class TransferIntraBankRequest
    {
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string customerReference { get; set; }
        public string feeType { get; set; }
        public string remark { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfoReq additionalInfo { get; set; }

        public TransferIntraBankRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoReq();
        }
    }

    public class AdditionalInfoReq
    {
        public string deviceId { get; set; }
        public string channel { get; set; }
        public string isRdn { get; set; }
    }

    public class TransferIntraBankResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }        
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string customerReference { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfoRes additionalInfo { get; set; }

        public TransferIntraBankResponse()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoRes();
        }

    }

    public class AdditionalInfoRes
    {
        public string channel { get; set; }
        public string deviceId { get; set; }
    }

    public class Amount
    {
        public string value { get; set; }
        public string currency { get; set; }
    }

    public class BIfastRequest
    {

        public string customerReference { get; set; }
        public string senderIdentityNumber { get; set; }
        public string sourceAccountNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string referenceNo { get; set; }
        public string externalId { get; set; }
        public string transactionDate { get; set; }
        public string paymentInfo { get; set; }
        public string senderType { get; set; }
        public string senderResidentStatus { get; set; }
        public string senderTownName { get; set; }
        public AdditionalInfoReq additionalInfo { get; set; }
        public BIfastRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoReq();
        }
    }

    public class BIfastResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string customerReference { get; set; }
        public string sourceAccountNo { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string referenceNo { get; set; }
        public string externalId { get; set; }
        public string journalSequence { get; set; }
        public string originalReferenceNo { get; set; }
        public Amount amount { get; set; }
        public AdditionalInfoRes additionalInfo { get; set; }

        public BIfastResponse()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoRes();
        }
    }

    public class BRI_OnlineTransfer
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
        public string TransactionDate { get; set; }
        public DateTime? SendTime { get; set; }
        public DateTime? ReceivedTime { get; set; }       
        public string ServiceCode { get; set; }
        public string PartnerReferenceNo { get; set; }
        public string CustomerReference { get; set; }
        public string DeviceId { get; set; }
        public string Channel { get; set;}
        public string IsRdn { get; set;}
        public string BeneficiaryBankCode { get; set; }
        public string Remark { get; set; }
        public string PaymentInfo { get; set; }
        public string SenderType { get; set; }
        public string SenderResidentStatus { get; set; }
        public string SenderTownName { get; set; }
        public string HTTPCode { get; set; }
        public string StatusCode { get; set; }
        public string StatusMessage { get; set; }



    }

    
}
