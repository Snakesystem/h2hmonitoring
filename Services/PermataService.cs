using H2HAPICore.Context;
using Dapper;
using H2HAPICore.Model.Permata;
using System.Data;
using System.Dynamic;
using Newtonsoft.Json;
using RestSharp;
using H2HAPICore.Model.S21BO;
using System.Net;
using static H2HAPICore.Model.Permata.TokenRequest;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.Xml;
using Microsoft.IdentityModel.Tokens;
using System;
using Azure.Core;
using static H2HAPICore.Services.H2HInject;

namespace H2HAPICore.Services
{
    public interface IPermataService
    {
        public Task<object> InvestorAccountStatement(InvestorAccountStatementRequest notifData, bool IsAuth);
        public Task<InvestorAccountStatementResponse> ManualPosting(int statementid);
        public Task<Token> GetToken();
        public Task<object> OnlineTransfer(int BankInstructionNID, bool bypass);

    }

    public class PermataService : IPermataService
    {
        private readonly DapperContext _context;
        private readonly IGenericService _genericService;
        private readonly ILoggerManager _logger;

        public PermataService(DapperContext context, ILoggerManager logger, IGenericService genericService)
        {
            _context = context;
            _logger = logger;
            _genericService = genericService;
        }

        #region Notification Deposit
        public async Task<object> InvestorAccountStatement(InvestorAccountStatementRequest notifData, bool IsAuth)
        {
            dynamic result = new ExpandoObject();
            _logger.LogInfo($"");
            _logger.LogInfo($"");
            _logger.LogInfo($"InvestorAccountStatement");

            InvestorAccountStatementResponse response = new InvestorAccountStatementResponse();

            if (!IsAuth)
            {
                response.NotificationTransactionRs.MsgRsHdr.CustRefID = notifData.NotificationTransactionRq.MsgRqHdr.CustRefID;
                response.NotificationTransactionRs.MsgRsHdr.ResponseTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+07:00";
                response.NotificationTransactionRs.MsgRsHdr.StatusCode = "01";
                response.NotificationTransactionRs.MsgRsHdr.StatusDesc = "Failed";
                result = JsonConvert.SerializeObject(response);

                return result;
            }

            response.NotificationTransactionRs.MsgRsHdr.CustRefID = notifData.NotificationTransactionRq.MsgRqHdr.CustRefID;
            response.NotificationTransactionRs.MsgRsHdr.ResponseTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+07:00";
            response.NotificationTransactionRs.MsgRsHdr.StatusCode = "00";
            response.NotificationTransactionRs.MsgRsHdr.StatusDesc = "Success";

            _logger.LogInfo("      EXT              : " + notifData.NotificationTransactionRq.TransactionInfo.Statements.ExtRef);
            _logger.LogInfo("      AccountNumber    : " + notifData.NotificationTransactionRq.TransactionInfo.AccountNumber);
            _logger.LogInfo("      Amount           : " + notifData.NotificationTransactionRq.TransactionInfo.Statements.CashValue);
            _logger.LogInfo("      txndate          : " + notifData.NotificationTransactionRq.MsgRqHdr.RequestTimestamp);
            _logger.LogInfo("      txntype          : " + notifData.NotificationTransactionRq.TransactionInfo.Statements.DC);

            bool skipped = false;
            string skipReason = "";
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    Settings settings = PermataUtility.permataSettings;

                    #region validation                   

                    if (notifData.NotificationTransactionRq.TransactionInfo.Notes.ToUpper().Contains(UtilityClass.ExcludeString))
                    {
                        skipped = true;
                        skipReason = $" EXCLUDE STRING {UtilityClass.ExcludeString}";
                    }

                    if (notifData.NotificationTransactionRq.TransactionInfo.Statements.DC == "D")
                    {
                        skipped = true;
                        skipReason = $" CREDIT/DEBIT {notifData.NotificationTransactionRq.TransactionInfo.Statements.Description}";
                    }

                    if (notifData.NotificationTransactionRq.TransactionInfo.Statements.Flag != "02")
                    {
                        if (notifData.NotificationTransactionRq.TransactionInfo.Statements.TrxType != "NTAX" && notifData.NotificationTransactionRq.TransactionInfo.Statements.TrxType != "NINT")
                        {
                            skipped = true;
                            skipReason = "TRX FROM BROKER";
                        }                        
                    }


                    if (!skipped)
                    {
                        var tmpAccStatement = await connectionH2H.QueryAsync("SELECT * FROM [PERMATA_AccountStatement] WHERE ExtRef = @ExtRef", new { ExtRef = notifData.NotificationTransactionRq.TransactionInfo.Statements.ExtRef });
                        if (tmpAccStatement.Count() > 0)
                        {
                            skipped = true;
                            skipReason = $" EXTERNAL REF ALREADY EXIST {notifData.NotificationTransactionRq.TransactionInfo.Statements.ExtRef}";
                        }
                        else
                        {
                            var tmpClient = await connectionBO.QueryAsync("SELECT * FROM [Client] WHERE SavingsID = @SavingsID", new { SavingsID = notifData.NotificationTransactionRq.TransactionInfo.AccountNumber});
                            if (tmpClient.Count() <= 0)
                            {
                                skipped = true;
                                skipReason = $" CLIENT NOT FOUND {notifData.NotificationTransactionRq.TransactionInfo.AccountNumber}";
                            }
                        }
                    }
                    #endregion

                    string query = $@" INSERT INTO [dbo].[PERMATA_AccountStatement]
                                    ([CashValue],[OpeningBalance],[CloseBal],[TxnDate],[ExtRef],[CustRefID],[GroupID],[SeqNum],[AccountNumber],[Currency]
                                    ,[ValueDate],[TrxType],[Flag],[DC],[Description],[Notes],[Status],[StatusReason],[inProc],[ReceiveTime])
                                    VALUES
                                    (@CashValue,@OpeningBalance,@CloseBal,@TxnDate,@ExtRef,@CustRefID,@GroupID,@SeqNum,@AccountNumber,@Currency
                                    ,@ValueDate,@TrxType,@Flag,@DC,@Description,@Notes,@Status,@StatusReason,@inProc,@ReceiveTime) SELECT scope_identity()";

                    PERMATA_AccountStatement obj = new PERMATA_AccountStatement(notifData);
                    obj.inProc = true;
                    obj.Status = 1;
                    if (skipped)
                    {
                        obj.Status = 2;
                        obj.inProc = false;
                        obj.StatusReason = skipReason;
                    }
                    obj.ReceiveTime = DateTime.Now;

                    obj.Statement_ID = await connectionH2H.ExecuteScalarAsync<long>(query, obj);
                    _logger.LogInfo($"   {obj.ExtRef} inserted ");

                    if (!skipped)
                    {
                        Thread threadobj = new Thread(new ParameterizedThreadStart(InjectBO));
                        threadobj.Start(obj);
                        result.NotificationTransactionRs.MsgRsHdr.ResponseTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+07:00";
                        
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"InvestorAccountStatement EX: {ex.Message}");
                response.NotificationTransactionRs.MsgRsHdr.ResponseTimestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+07:00";
            }

            result = JsonConvert.SerializeObject(response);

            return result;
        }
        public async Task<InvestorAccountStatementResponse> ManualPosting(int statementid)
        {
            _logger.LogInfo($"");
            _logger.LogInfo($"--------------------MANUAL POSTING-------------------------");
            InvestorAccountStatementResponse result = new InvestorAccountStatementResponse();
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {

                    string query = $@"SELECT * FROM [PERMATA_AccountStatement] WHERE Statement_ID = @Statement_ID";
                    PERMATA_AccountStatement obj = (await connectionH2H.QueryAsync<PERMATA_AccountStatement>(query, new { Statement_ID = statementid })).SingleOrDefault();

                    var tmpFundInOut = await connectionBO.QueryAsync("SELECT * FROM [FundInOut] WHERE Revised = 0 AND Rejected = 0 AND DocumentRef = @DocumentRef", new { DocumentRef = obj.ExtRef });
                    if (tmpFundInOut.Count() > 0)
                    {
                        _logger.LogInfo($"  CANCEL MANUAL POSTING, ExternalRef ALREADY EXISTS " + obj.ExtRef);
                        result.NotificationTransactionRs.MsgRsHdr.StatusCode = "1";
                        return result;
                    }

                    _logger.LogInfo("      AccountNumber   : " + obj.AccountNumber);
                    _logger.LogInfo("      Amount   : " + obj.CashValue);
                    _logger.LogInfo("      txndate    : " + obj.TxnDate);
                    _logger.LogInfo("      Extref    : " + obj.ExtRef);

                    InjectBO(obj);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"InvestorAccountStatement EX: {ex.Message}");
            }
            _logger.LogInfo($"--------------------MANUAL POSTING END-------------------------");
            return result;
        }
        public async void InjectBO(object data)
        {
            try
            {
                PERMATA_AccountStatement obj = (PERMATA_AccountStatement)data;


                int year = Convert.ToInt32(obj.CustRefID.Substring(0, 4));
                int month = Convert.ToInt32(obj.CustRefID.Substring(4, 2));
                int day = Convert.ToInt32(obj.CustRefID.Substring(6, 2));

                int hour = Convert.ToInt32(obj.CustRefID.Substring(8, 2));
                int min = Convert.ToInt32(obj.CustRefID.Substring(10, 2));
                int sec = Convert.ToInt32(obj.CustRefID.Substring(12, 2));

                DateTime bcadate = new DateTime(year, month, day, hour, min, sec);
                H2HInject.Request fundin = new H2HInject.Request();
                fundin.DC = obj.DC;
                fundin.BankNID = 5;
                fundin.BankName = "PT. BANK PERMATA";
                fundin.accountNumber = obj.AccountNumber;
                fundin.amount = obj.CashValue;
                fundin.ExtRef = obj.ExtRef;
                fundin.TrxType = obj.TxnDate;
                fundin.transactionDate = bcadate;
                string query = string.Empty;

                H2HInject.Response response = await _genericService.InsertFundInOut(fundin);
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    if (response.result)
                    {
                        query = $@"UPDATE [PERMATA_AccountStatement]
                                       SET FundInOutNID = @FundInOutNID, FundNID = @FundNID, ClientNID = @ClientNID, ClientID = @ClientID, inProc = 0
                                        WHERE Statement_ID = @Statement_ID";

                        DynamicParameters p = new DynamicParameters();
                        p.Add("@FundInOutNID", response.FundInOutNID);
                        p.Add("@FundNID", response.FundNID);
                        p.Add("@ClientNID", response.ClientNID);
                        p.Add("@ClientID", response.ClientID);
                        p.Add("@Statement_ID", obj.Statement_ID);
                        await connectionH2H.ExecuteAsync(query, p);
                    }
                    else
                    {
                        query = $@"UPDATE [PERMATA_AccountStatement]
                                       SET inProc = 0 WHERE Statement_ID = @Statement_ID";

                        DynamicParameters p = new DynamicParameters();
                        p.Add("@Statement_ID", obj.Statement_ID);
                        await connectionH2H.ExecuteAsync(query, p);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogInfo($"      *** EXC injectBO " + ex.Message);
            }
        }
        #endregion

        #region Withdrawal Instruction
        public async Task<Token> GetToken()
        {
            Token result = new Token();
            TokenRequest objReq = new TokenRequest();
            Settings settings = PermataUtility.permataSettings;

            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "+07:00";
            string stringToSign = PermataUtility.permataSettings.clientid + "|" + timestamp;
            string signature = PermataUtility.GetSignature(stringToSign);

            try
            {
                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("X-TIMESTAMP", timestamp);
                client.AddDefaultHeader("X-CLIENT-KEY", PermataUtility.permataSettings.clientid);
                client.AddDefaultHeader("X-SIGNATURE", signature);

                objReq.grantType = "client_credentials";

                string RequestBody = JsonConvert.SerializeObject(objReq);

                var Request = new RestRequest();
                Request.Method = Method.Post;
                Request.Resource = "/openapi/v1.0/access-token/b2b";
                Request.AddJsonBody(RequestBody);

                RestResponse response = client.Execute(Request);
                string body = response.Content;
                result = JsonConvert.DeserializeObject<Token>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(" ******** EXC getTokenPERMATA : " + ex.Message);
            }

            return result;
        }
        public async Task<object> OnlineTransfer(int BankInstructionNID, bool bypass)
        {
            dynamic result = new ExpandoObject();
            _logger.LogInfo("");
            _logger.LogInfo("START ONLINE TRANSFER " + BankInstructionNID);
            _logger.LogInfo("==========================");
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    var queryIns = "SELECT SourceSavingsID, SourceSavingsName, SourceBankAccountID, TargetSavingsID, TargetSavingsName,TargetBankAccountID, Amount, BatchID, ClientID, Success, [Type] FROM [dbo].[BankInstruction] WHERE BankInstructionNID = @BankInstructionNID";

                    var BankInstruction = await connectionBO.QuerySingleAsync(queryIns, new { BankInstructionNID = BankInstructionNID });


                    if (BankInstruction.BatchID > 0 && BankInstruction.Success)
                    {
                        if (!bypass)
                        {
                            _logger.LogInfo("BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID);
                            _logger.LogInfo("END ONLINE TRANSFER " + BankInstructionNID);
                            _logger.LogInfo("==========================");
                            result.responseMessage = $"BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID; 
                            return result;
                        }
                    }

                    var queryH2H = "SELECT BankInstructionNID, BatchID, HTTPCode FROM [dbo].[PERMATA_AccountBankInstruction] WHERE BankInstructionNID = @BankInstructionNID";

                    var H2HInstruction = await connectionH2H.QueryAsync(queryH2H, new { BankInstructionNID = BankInstructionNID });

                    if (H2HInstruction.Any())
                    {
                        dynamic temp = null;
                        temp = H2HInstruction.ToList();

                        foreach (dynamic item in temp)
                        {
                            if (item.BatchID > 0 && item.HTTPCode == "OK")
                            {
                                _logger.LogInfo("BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID);
                                _logger.LogInfo("END ONLINE TRANSFER " + BankInstructionNID);
                                _logger.LogInfo("==========================");
                                result.responseMessage = $"BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID;
                                return result;
                            }                       
                        }
                    }

                    var queryClient = "SELECT [ClientNID],[Email],[Address] FROM [Client] WHERE ClientID = @ClientID";

                    var Client = await connectionBO.QuerySingleAsync(queryClient, new { ClientID = BankInstruction.ClientID });

                    int BatchID = BankInstruction.BatchID;
                    if (BatchID == 0)
                    {
                        BatchID = (await connectionBO.QueryAsync<int>("SELECT MAX(BatchID)+1 as BatchID From [dbo].[BankInstruction]")).SingleOrDefault();
                    }


                    RestResponse response = new RestResponse();
                    PERMATA_OnlineTransfer objbank = new PERMATA_OnlineTransfer();

                    string Timestamp = UtilityClass.GetTimestamp();
                    RandomString Random = await UtilityClass.GetRandomString();
                    string ServiceCode = "";

                    if (BankInstruction.TargetBankAccountID == BankInstruction.SourceBankAccountID)
                    {
                        ServiceCode = "TRANSFER INTRA BANK";

                        PermataOverBookRequest req = new PermataOverBookRequest();
                        req.partnerReferenceNo = Random.PartnerReferenceNo;
                        req.amount.value = BankInstruction.Amount.ToString("0.00");
                        req.amount.currency = "IDR";
                        req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;
                        req.beneficiaryEmail = Client.Email;
                        if (!string.IsNullOrEmpty(Client.Email))
                        {
                            string[] emails = Client.Email.Split(";".ToCharArray());
                            if (emails.Count() > 0)
                            {
                                req.beneficiaryEmail = emails[0];
                            }
                        }
                        req.currency = "IDR";
                        req.customerReference = Random.CustomerReference;
                        req.remark = "$S21$-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                        req.sourceAccountNo = BankInstruction.SourceSavingsID;
                        req.transactionDate = Timestamp;
                        req.additionalInfo.sourceAccountName = BankInstruction.SourceSavingsName;
                        req.additionalInfo.beneficiaryAccountName = BankInstruction.TargetSavingsName;                                               

                        objbank.BankInstructionNID = BankInstructionNID;
                        objbank.BatchID = 0;
                        objbank.ClientNID = Client.ClientNID;
                        objbank.ClientID = BankInstruction.ClientID;
                        objbank.Amount = req.amount.value;
                        objbank.SourceAccountNo = req.sourceAccountNo;
                        objbank.SourceAccountName = req.additionalInfo.sourceAccountName;
                        objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                        objbank.BeneficiaryAccountName = req.additionalInfo.beneficiaryAccountName;
                        objbank.Type = BankInstruction.Type;
                        objbank.SendTime = DateTime.Now;

                        response = await sendToBank(Method.Post, "/apiservice/snp/intrabank/v1.0/transfer-intrabank", req, Timestamp, Random.ExternalID, ServiceCode);
                        result = JsonConvert.DeserializeObject<PermataOverBookResponse>(response.Content);

                        objbank.TransactionDate = req.transactionDate;
                        objbank.ReceivedTime = DateTime.Now;                       
                        objbank.ServiceCode = ServiceCode;
                        objbank.BeneficiaryEmail = req.beneficiaryEmail;
                        objbank.BeneficiaryBankName = "";
                        objbank.BeneficiaryAccountType = "";
                        objbank.BeneficiaryCustomerIdNumber = "";
                        objbank.BeneficiaryCustomerType = "";
                        objbank.BeneficiaryCustomerResidentStatus = "";
                        objbank.BeneficiaryCustomerTownName = "";
                        objbank.BeneficiaryBankCode = "";
                        objbank.BeneficiaryAddress = "";
                        objbank.BeneficiaryCitizenStatus = "";
                        objbank.BeneficiaryCustomerResidence = "";
                        objbank.PartnerReferenceNo = req.partnerReferenceNo;
                        objbank.Remark = req.remark;                       
                        objbank.Memo = "";
                        objbank.HTTPCode = response.StatusCode.ToString();
                        objbank.StatusCode = result.responseCode;
                        objbank.StatusMessage = result.responseMessage;

                    }
                    else
                    {
                        
                        var queryBankCode = "SELECT PaymentBankID,Is_ONL, Is_BIFAST, Is_LLG, Is_RTGS, BankCode, BICCode, BankName FROM [dbo].[ListBankCode] WHERE PaymentBankID = @PaymentBankID";

                        var targetBankCode = await connectionH2H.QuerySingleAsync(queryBankCode, new { PaymentBankID = BankInstruction.TargetBankAccountID });

                        string URL = "";

                        if (BankInstruction.Amount < 100000000 && targetBankCode.Is_BIFAST)
                        {
                            ServiceCode = "BIFAST";

                            PermataBIFASTRequest req = new PermataBIFASTRequest();

                            req.partnerReferenceNo = Random.PartnerReferenceNo;
                            req.amount.value = BankInstruction.Amount.ToString("0.00");
                            req.amount.currency = "IDR";
                            req.beneficiaryAccountName = BankInstruction.TargetSavingsName;
                            req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;
                            req.beneficiaryBankCode = targetBankCode.BICCode;
                            req.sourceAccountNo = BankInstruction.SourceSavingsID;
                            req.transactionDate = Timestamp;
                            req.additionalInfo.sourceAccountName = BankInstruction.SourceSavingsID;
                            req.additionalInfo.beneficiaryBankName = targetBankCode.BankName;
                            req.additionalInfo.chargeBearerCode = "DEBT";
                            req.additionalInfo.beneficiaryAccountType = "SVGS";
                            req.additionalInfo.purposeOfTransaction = "01";
                            req.additionalInfo.beneficiaryCustomerIdNumber = "";
                            req.additionalInfo.beneficiaryCustomerType = "01";
                            req.additionalInfo.beneficiaryCustomerResidentStatus = "";
                            req.additionalInfo.beneficiaryCustomerTownName = "";
                            req.additionalInfo.memo = "$S21$-" + BankInstruction.ClientID + "-" + BankInstructionNID;

                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = 0;
                            objbank.ClientNID = Client.ClientNID;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.Amount = req.amount.value;
                            objbank.SourceAccountNo = req.sourceAccountNo;
                            objbank.SourceAccountName = req.additionalInfo.sourceAccountName;
                            objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                            objbank.BeneficiaryAccountName = req.beneficiaryAccountName;
                            objbank.Type = BankInstruction.Type;
                            objbank.TransactionDate = req.transactionDate;
                            objbank.SendTime = DateTime.Now;

                            response = await sendToBank(Method.Post, "/apiservice/snp/interbank/v1.0/transfer-interbank", req, Timestamp, Random.ExternalID, ServiceCode);
                            result = JsonConvert.DeserializeObject<PermataBIFASTResponse>(response.Content);

                            objbank.ReceivedTime = DateTime.Now;
                            objbank.ServiceCode = ServiceCode;
                            objbank.BeneficiaryEmail = "";
                            objbank.BeneficiaryBankName = req.additionalInfo.beneficiaryBankName;
                            objbank.BeneficiaryAccountType = req.additionalInfo.beneficiaryAccountType;
                            objbank.BeneficiaryCustomerIdNumber = req.additionalInfo.beneficiaryCustomerIdNumber;
                            objbank.BeneficiaryCustomerType = req.additionalInfo.beneficiaryCustomerType;
                            objbank.BeneficiaryCustomerResidentStatus = req.additionalInfo.beneficiaryCustomerResidentStatus;
                            objbank.BeneficiaryCustomerTownName = req.additionalInfo.beneficiaryCustomerTownName;
                            objbank.BeneficiaryBankCode = req.beneficiaryBankCode;
                            objbank.BeneficiaryAddress = "";
                            objbank.BeneficiaryCitizenStatus = "";
                            objbank.BeneficiaryCustomerResidence = "";
                            objbank.PartnerReferenceNo = req.partnerReferenceNo;
                            objbank.Remark = "";
                            objbank.Memo = "";
                            objbank.HTTPCode = response.StatusCode.ToString();
                            objbank.StatusCode = result.responseCode;
                            objbank.StatusMessage = result.responseMessage;

                        }
                        else
                        {

                            PermataLLGRTGSRequest req = new PermataLLGRTGSRequest();
                            req.partnerReferenceNo = Random.PartnerReferenceNo;
                            req.amount.value = BankInstruction.Amount.ToString("0.00");
                            req.amount.currency = "IDR";
                            req.beneficiaryAccountName = BankInstruction.TargetSavingsName;
                            req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;
                            req.beneficiaryAddress = Client.Address;
                            if (Client.Address.Length > 40)
                            {
                                req.beneficiaryAddress = Client.Address.Substring(0, 40);
                            }
                            req.beneficiaryBankCode = targetBankCode.BankCode;
                            req.beneficiaryBankName = targetBankCode.BankName;
                            req.beneficiaryCustomerResidence = "0";
                            req.beneficiaryCustomerType = "1";
                            req.beneficiaryEmail = Client.Email;
                            if (!string.IsNullOrEmpty(Client.Email))
                            {
                                string[] emails = Client.Email.Split(";".ToCharArray());
                                if (emails.Count() > 0)
                                {
                                    req.beneficiaryEmail = emails[0];
                                }
                            }
                            req.currency = "IDR";
                            req.feeType = "0";
                            req.remark = "$S21$-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                            req.sourceAccountNo = BankInstruction.SourceSavingsID;
                            req.transactionDate = Timestamp;
                            req.additionalInfo.beneficiaryCitizenStatus = "0";
                            req.additionalInfo.sourceAccountName = BankInstruction.SourceSavingsName;

                            if (BankInstruction.Amount < 100000000)
                            {
                                ServiceCode = "LLG";
                                URL = "/apiservice/snp/llgtransfer/v1.0/transfer-skn";
                            }
                            else
                            {
                                ServiceCode = "RTGS";
                                URL = "/apiservice/snp/rtgstransfer/v1.0/transfer-rtgs";
                            }

                            _logger.LogInfo("");
                            _logger.LogInfo($"ONLINE TRANSFER {ServiceCode} " + BankInstructionNID);
                            _logger.LogInfo("==========================");

                            
                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = 0;
                            objbank.ClientNID = Client.ClientNID;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.Amount = req.amount.value;
                            objbank.SourceAccountNo = req.sourceAccountNo;
                            objbank.SourceAccountName = req.additionalInfo.sourceAccountName;
                            objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                            objbank.BeneficiaryAccountName = req.beneficiaryAccountName;
                            objbank.Type = BankInstruction.Type;
                            objbank.TransactionDate = req.transactionDate;
                            objbank.SendTime = DateTime.Now;

                            response = await sendToBank(Method.Post, URL, req, Timestamp, Random.ExternalID, ServiceCode);
                            result = JsonConvert.DeserializeObject<PermataLLGRTGSResponse>(response.Content);

                            objbank.ReceivedTime = DateTime.Now;                            
                            objbank.ServiceCode = ServiceCode;
                            objbank.BeneficiaryEmail = req.beneficiaryEmail;
                            objbank.BeneficiaryBankName = req.beneficiaryBankName;
                            objbank.BeneficiaryAccountType = "";
                            objbank.BeneficiaryCustomerIdNumber = "";
                            objbank.BeneficiaryCustomerType = req.beneficiaryCustomerType;
                            objbank.BeneficiaryCustomerResidentStatus = "";
                            objbank.BeneficiaryCustomerTownName = "";
                            objbank.BeneficiaryBankCode = req.beneficiaryBankCode;
                            objbank.BeneficiaryAddress = req.beneficiaryAddress;
                            objbank.BeneficiaryCitizenStatus = req.additionalInfo.beneficiaryCitizenStatus;
                            objbank.BeneficiaryCustomerResidence = req.beneficiaryCustomerResidence;
                            objbank.PartnerReferenceNo = req.partnerReferenceNo;
                            objbank.Remark = req.remark;                           
                            objbank.Memo = "";
                            objbank.HTTPCode = response.StatusCode.ToString();
                            objbank.StatusCode = result.responseCode;
                            objbank.StatusMessage = result.responseMessage;

                        }                        
                    
                    }

                    string query = @"INSERT INTO [dbo].[PERMATA_OnlineTransfer]
                                        ([BankInstructionNID],[BatchID],[ClientNID],[ClientID],[Amount],[SourceAccountNo],[SourceAccountName],[BeneficiaryAccountNo],[BeneficiaryAccountName]
                                        ,[Type],[SendTime],[TransactionDate],[ReceivedTime],[ServiceCode],[BeneficiaryEmail],[BeneficiaryBankName],[BeneficiaryAccountType],[BeneficiaryCustomerIdNumber]
                                        ,[BeneficiaryCustomerType],[BeneficiaryCustomerResidentStatus],[BeneficiaryCustomerTownName],[BeneficiaryBankCode],[BeneficiaryAddress]
                                        ,[BeneficiaryCitizenStatus],[BeneficiaryCustomerResidence],[PartnerReferenceNo],[Remark],[Memo],[HTTPCode],[StatusCode],[StatusMessage])
                                         VALUES
                                        (@BankInstructionNID,@BatchID,@ClientNID,@ClientID,@Amount,@SourceAccountNo,@SourceAccountName,@BeneficiaryAccountNo,@BeneficiaryAccountName
                                        ,@Type,@SendTime,@TransactionDate,@ReceivedTime,@ServiceCode,@BeneficiaryEmail,@BeneficiaryBankName,@BeneficiaryAccountType,@BeneficiaryCustomerIdNumber                                        
                                        ,@BeneficiaryCustomerType,@BeneficiaryCustomerResidentStatus,@BeneficiaryCustomerTownName,@BeneficiaryBankCode,@BeneficiaryAddress
                                        ,@BeneficiaryCitizenStatus,@BeneficiaryCustomerResidence,@PartnerReferenceNo,@Remark,@Memo,@HTTPCode,@StatusCode,@StatusMessage) SELECT scope_identity()";

                    objbank.AutoNID = await connectionH2H.ExecuteScalarAsync<long>(query, objbank);
                    _logger.LogInfo("         [PERMATA_OnlineTransfer] INSERTED " + objbank.AutoNID);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        query = "UPDATE [PERMATA_AccountBankInstruction] SET BatchID = @BatchID WHERE BankInstructionNID = @BankInstructionNID AND AutoNID = @AutoNID";
                        var ph2h = new DynamicParameters();
                        ph2h.Add("@AutoNID", objbank.AutoNID);
                        ph2h.Add("@BatchID", BatchID);
                        ph2h.Add("@BankInstructionNID", BankInstructionNID);
                        await connectionH2H.ExecuteAsync(query, ph2h);

                        _logger.LogInfo("         [PERMATA_OnlineTransfer] UPDATE " + objbank.AutoNID);


                        if (BankInstruction.BatchID != BatchID)
                        {
                            query = "UPDATE [BankInstruction] SET BatchID = @BatchID, Success = @Success, LastUpdate = @LastUpdate, Rejected = @Rejected WHERE BankInstructionNID = @BankInstructionNID";
                            var p = new DynamicParameters();
                            p.Add("@BatchID", BatchID);
                            p.Add("@Success", true);
                            p.Add("@Rejected", false);
                            p.Add("@LastUpdate", DateTime.Now);
                            p.Add("@BankInstructionNID", BankInstructionNID);
                            await connectionBO.ExecuteAsync(query, p);

                            _logger.LogInfo("         [BankInstruction] UPDATE " + BankInstructionNID);
                        }  
                    }
                    else
                    {
                        query = "UPDATE [BankInstruction] SET BatchID = @BatchID, Success = @Success, LastUpdate = @LastUpdate, Rejected = @Rejected WHERE BankInstructionNID = @BankInstructionNID";
                        var p = new DynamicParameters();
                        p.Add("@BatchID", 0);
                        p.Add("@Success", false);
                        p.Add("@Rejected", true);
                        p.Add("@LastUpdate", DateTime.Now);
                        p.Add("@BankInstructionNID", BankInstructionNID);
                        await connectionBO.ExecuteAsync(query, p);

                        _logger.LogInfo("         [BankInstruction] UPDATE " + BankInstructionNID);
                    }

                    BankInstructionResult objInstructionResult = new BankInstructionResult();
                    objInstructionResult.Date = DateTime.Now;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        objInstructionResult.BatchID = BatchID;
                    }
                    else
                    {
                        objInstructionResult.BatchID = 0;
                    }                        
                    objInstructionResult.AutoID = 1;
                    objInstructionResult.LastUpdate = DateTime.Now;
                    objInstructionResult.Bank = BankInstruction.SourceBankAccountID;
                    if (BankInstruction.Type == "CRE")
                    {
                        objInstructionResult.Action = "Credit";
                    }
                    else if (BankInstruction.Type == "COL")
                    {
                        objInstructionResult.Action = "Collection";
                    }
                    else
                    {
                        objInstructionResult.Action = "Private";
                    }

                    objInstructionResult.SysUserID = UtilityClass.SysUserID;
                    objInstructionResult.TerminalID = UtilityClass.CompH2H;
                    objInstructionResult.UserNID = UtilityClass.UserMaker;
                    objInstructionResult.ResultText = response.Content;
                    objInstructionResult.FileName = "";

                    string queryResult = @"INSERT INTO [dbo].[BankInstructionResult]
                                    ([Date],[Bank],[Action],[BatchID],[AutoID],[FileName]
                                    ,[ResultText],[LastUpdate],[UserNID],[SysUserID],[TerminalID])
                                         VALUES
                                    (@Date,@Bank,@Action,@BatchID,@AutoID,@FileName
                                    ,@ResultText,@LastUpdate,@UserNID,@SysUserID,@TerminalID)";

                    await connectionBO.ExecuteAsync(queryResult, objInstructionResult);
                    _logger.LogInfo("         [BankInstructionResult] INSERTED " + objInstructionResult.BatchID);

                }

            }
            catch (Exception ex)
            {
                _logger.LogError(" **** EXC OnlineTransfer " + ex.Message);
            }
            _logger.LogInfo("END ONLINE TRANSFER " + BankInstructionNID);
            _logger.LogInfo("==========================");
            return result;
        }
        public async Task<RestResponse> sendToBank(Method method, string url, object body, string timestamp, string randomExternal, string servicecode)
        {
            RestResponse result = new RestResponse();
            _logger.LogInfo("SEND TO BANK PERMATA");
            _logger.LogInfo("==========================");
            _logger.LogInfo($"SERVICE {servicecode}");
            try
            {

                string RequestBody = JsonConvert.SerializeObject(body);
                string payload = RequestBody.Replace("\r", "");
                payload = payload.Replace("\n", "");
                payload = payload.Replace("\t", "");

                Token permataToken = await GetToken();

                Settings settings = PermataUtility.permataSettings;

                string StringtoSign = await PermataUtility.StringtoSign(method.ToString().ToUpper(), url, permataToken.accessToken, payload, timestamp);
                string SignatureService = await PermataUtility.GetSignatureService(settings.clientsecret, StringtoSign);


                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("Authorization", "Bearer " + permataToken.accessToken);
                client.AddDefaultHeader("X-TIMESTAMP", timestamp);
                client.AddDefaultHeader("X-SIGNATURE", SignatureService);
                client.AddDefaultHeader("X-PARTNER-ID", settings.PARTNERID);
                client.AddDefaultHeader("X-EXTERNAL-ID", randomExternal);
                client.AddDefaultHeader("CHANNEL-ID", "865");

                var Request = new RestRequest();
                Request.Method = Method.Post;
                Request.Resource = url;
                Request.AddJsonBody(RequestBody);
                _logger.LogInfo("==========================");
                _logger.LogInfo($"         [Request] {RequestBody}");
                _logger.LogInfo("==========================");

                RestResponse response = client.Execute(Request);
                _logger.LogInfo($"         [Response] {response.Content}");
                _logger.LogInfo("==========================");

                return response;

            }
            catch (Exception ex)
            {
                _logger.LogError(" **** EXC SEND TO BANK PERMATA : " + ex.Message);
            }

            return result;
        }
        #endregion
    }



}
