using H2HAPICore.Model.Permata;
using System.Globalization;
using System.Text;
using static H2HAPICore.Model.Permata.TokenRequest;

namespace H2HAPICore.Model.BCA
{
    public class Token
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string accessToken { get; set; }
        public string tokenType { get; set; }
        public string expiresIn { get; set; }
    }

    public class CreateToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public double expires_in { get; set; }
        public string scope { get; set; }
    }

    public class TokenBCA
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public string expires_in { get; set; }
        public string scope { get; set; }
    }

    public class InvestorAccountStatementResponse
    {
        public string ResponseWS { get; set; }
    }

    public class Credentials
    {
        public string clientid { get; set; }
        public string clientsecret { get; set; }
        public string apikey { get; set; }
        public string apisecret { get; set; }
        public string PARTNERID { get; set; }
    }

    public class Settings
    {
        public string URL { get; set; }
        public string ExcludeString { get; set; }
        public bool DepositOnly { get; set; }
        public bool DisableTaxInterest { get; set; }
        public double ExpireToken { get; set; }
        public Credentials InBound { get; set; }
        public Credentials OutBound { get; set; }
        public Credentials RDN { get; set; }
    }

    public class InvestorAccountStatementRequest
    {
        public string ExternalReference { get; set; }
        public string SeqNumber { get; set; }
        public string AccountNumber { get; set; }
        public string Currency { get; set; }
        public string TxnDate { get; set; }
        public string TxnType { get; set; }
        public string TxnCode { get; set; }
        public string AccountDebit { get; set; }
        public string AccountCredit { get; set; }
        public string TxnAmount { get; set; }
        public string OpenBalance { get; set; }
        public string CloseBalance { get; set; }
        public string TxnDesc { get; set; }

        public string parseBody()
        {
            StringBuilder sb = new StringBuilder();
            string _txndesc = TxnDesc.Replace("\\", "\\\\");
            sb.Append("{");
            sb.Append("\"ExternalReference\":\"" + ExternalReference + "\",");
            sb.Append("\"SeqNumber\":\"" + SeqNumber + "\",");
            sb.Append("\"AccountNumber\":\"" + AccountNumber + "\",");
            sb.Append("\"Currency\":\"" + Currency + "\",");
            sb.Append("\"TxnDate\":\"" + TxnDate + "\",");
            sb.Append("\"TxnType\":\"" + TxnType + "\",");
            sb.Append("\"TxnCode\":\"" + TxnCode + "\",");
            sb.Append("\"AccountDebit\":\"" + AccountDebit + "\",");
            sb.Append("\"AccountCredit\":\"" + AccountCredit + "\",");
            sb.Append("\"TxnAmount\":\"" + TxnAmount + "\",");
            sb.Append("\"OpenBalance\":\"" + OpenBalance + "\",");
            sb.Append("\"CloseBalance\":\"" + CloseBalance + "\",");
            sb.Append("\"TxnDesc\":\"" + _txndesc + "\"");
            sb.Append("}");
            return sb.ToString();
        }

        public string parseBody2()
        {
            StringBuilder sb = new StringBuilder();
            string _txndesc = TxnDesc.Replace("\\", "\\\\");
            sb.Append("{");
            sb.Append("\"AccountDebit\":\"" + AccountDebit + "\",");
            sb.Append("\"TxnAmount\":\"" + TxnAmount + "\",");
            sb.Append("\"OpenBalance\":\"" + OpenBalance + "\",");
            sb.Append("\"ExternalReference\":\"" + ExternalReference + "\",");
            sb.Append("\"CloseBalance\":\"" + CloseBalance + "\",");
            sb.Append("\"AccountCredit\":\"" + AccountCredit + "\",");
            sb.Append("\"AccountNumber\":\"" + AccountNumber + "\",");
            sb.Append("\"TxnCode\":\"" + TxnCode + "\",");
            sb.Append("\"Currency\":\"" + Currency + "\",");
            sb.Append("\"TxnDate\":\"" + TxnDate + "\",");
            sb.Append("\"SeqNumber\":\"" + SeqNumber + "\",");
            sb.Append("\"TxnDesc\":\"" + _txndesc + "\",");
            sb.Append("\"TxnType\":\"" + TxnType + "\"");
            sb.Append("}");
            return sb.ToString();
        }
    }

    public class BCA_AccountStatement
    {
        public long Statement_ID { get; set; }

        public string TxnAmount { get; set; }

        public string OpenBalance { get; set; }

        public string CloseBalance { get; set; }

        public string TxnDate { get; set; }

        public string ExternalRef { get; set; }

        public string SeqNumber { get; set; }

        public string AccountNumber { get; set; }

        public string Currency { get; set; }

        public string TxnType { get; set; }

        public string TxnCode { get; set; }

        public string AccountDebit { get; set; }

        public string AccountCredit { get; set; }

        public string TxnDesc { get; set; }

        public int? Status { get; set; }
        public string StatusReason { get; set; }

        public int? FundInOutNID { get; set; }

        public int? FundNID { get; set; }

        public bool? inProc { get; set; }

        public string ClientID { get; set; }
        public string ClientNID { get; set; }

        public DateTime? ReceiveTime { get; set; }

        public BCA_AccountStatement() { }

        public BCA_AccountStatement(InvestorAccountStatementRequest req)
        {
            TxnAmount = req.TxnAmount;
            OpenBalance = req.OpenBalance;
            CloseBalance = req.CloseBalance;
            TxnDate = req.TxnDate;
            ExternalRef = req.ExternalReference;
            SeqNumber = req.SeqNumber;
            AccountNumber = req.AccountNumber;
            Currency = req.Currency;
            TxnType = req.TxnType;
            TxnCode = req.TxnCode;
            AccountDebit = req.AccountDebit;
            AccountCredit = req.AccountCredit;
            TxnDesc = req.TxnDesc;
            inProc = false;
            ReceiveTime = DateTime.Now;
        }
    }

    public class TokenCreateRequest
    {
        public string grant_type { get; set; }
    }
    public class TokenRequest
    {
        public string grantType { get; set; }
    }

    public class BCA_OnlineTransfer
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
        public string PartnerReferenceNo { get; set; }
        public string CustomerReference { get; set; }
        public string FeeType { get; set; }
        public string Remark { get; set; }
        public string EconomicActivity { get; set; }
        public string TransactionPurpose { get; set; }
        public string BeneficiaryEmail { get; set; }
        public string BeneficiaryBankName { get; set; }
        public string BeneficiaryBankCode { get; set; }
        public string BeneficiaryAddress { get; set; }
        public string TransferType { get; set; }
        public string PurposeCode { get; set; }
        public string BeneficiaryCustomerResidence { get; set; }
        public string BeneficiaryCustomerType { get; set; }
        public string KodePos { get; set; }
        public string ReceiverPhone { get; set; }
        public string SenderCustomerResidence { get; set; }
        public string SenderCustomerType { get; set; }
        public string SenderPhone { get; set; }
    }   

    public class TransferIntraBankRequest
    {
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryEmail { get; set; }
        public string currency { get; set; }
        public string customerReference { get; set; }
        public string feeType { get; set; }
        public string remark { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfoTransfer additionalInfo { get; set; }

        public TransferIntraBankRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoTransfer();
        }

    }

    public class Amount
    {
        public string value { get; set; }
        public string currency { get; set; }
    }

    public class AdditionalInfoTransfer
    {
        public string economicActivity { get; set; }
        public string transactionPurpose { get; set; }
    }

    public class TransferIntraBankResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string currency { get; set; }
        public string customerReference { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfoTransfer additionalInfo { get; set; }

        public TransferIntraBankResponse()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfoTransfer();
        }
    }

    public class BIFastRequest
    {
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountName { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string currency { get; set; }
        public string beneficiaryEmail { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }
        public AdditionalInfo additionalInfo { get; set; }

        public BIFastRequest()
        {
            amount = new Amount();
            additionalInfo = new AdditionalInfo();
        }

    }

    public class AdditionalInfo
    {
        public string transferType { get; set; }
        public string purposeCode { get; set; }
    }

    public class BIFastRequestResponse
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

    public class LLGRTGSRequest
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
        public string customerReference { get; set; }
        public string feeType { get; set; }
        public string kodepos { get; set; }
        public string receiverPhone { get; set; }
        public string remark { get; set; }
        public string senderCustomerResidence { get; set; }
        public string senderCustomerType { get; set; }
        public string senderPhone { get; set; }
        public string sourceAccountNo { get; set; }
        public string transactionDate { get; set; }

        public LLGRTGSRequest()
        {
            amount = new Amount();
        }

    }

    public class LLGRTGSResponse
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string referenceNo { get; set; }
        public string partnerReferenceNo { get; set; }
        public Amount amount { get; set; }
        public string beneficiaryAccountName { get; set; }
        public string beneficiaryAccountNo { get; set; }
        public string beneficiaryAccountType { get; set; }
        public string beneficiaryBankCode { get; set; }
        public string currency { get; set; }
        public string sourceAccountNo { get; set; }
        public string traceNo { get; set; }
        public string transactionDate { get; set; }
        public string transactionStatus { get; set; }
        public string transactionStatusDesc { get; set; }
        public LLGRTGSResponse()
        {
            amount = new Amount();
        }
    }
}
