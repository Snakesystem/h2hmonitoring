using H2HAPICore.Context;
using Dapper;
using H2HAPICore.Model.BRI;
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
using H2HAPICore.Controllers;
using System.Data.SqlTypes;
using Microsoft.AspNetCore.Identity;

namespace H2HAPICore.Services
{
    public interface IBRIService
    {
        public Task<InvestorAccountStatementResponse> InvestorAccountStatement(InvestorAccountStatementRequest notifData);
        public Task<InvestorAccountStatementResponse> ManualPosting(int statementid);
        public Task<Token> GetToken();
        public Task<InquiryBifastResponse> InquiryBifast(int BankInstructionNID, string AccountNo, string BankCode);
        public Task<object> OnlineTransfer(int BankInstructionNID, bool bypass);

    }

    public class BRIService : IBRIService
    {
        private readonly DapperContext _context;
        private readonly IGenericService _genericService;
        private readonly ILoggerManager _logger;

        public BRIService(DapperContext context, ILoggerManager logger, IGenericService genericService)
        {
            _context = context;
            _logger = logger;
            _genericService = genericService;
        }

        #region Notification Deposit
        public async Task<InvestorAccountStatementResponse> InvestorAccountStatement(InvestorAccountStatementRequest notifData)
        {
            _logger.LogInfo($"");
            _logger.LogInfo($"");
            _logger.LogInfo($"InvestorAccountStatement");

            InvestorAccountStatementResponse result = new InvestorAccountStatementResponse();
            result.data.externalReference = notifData.externalReference;
            result.data.idTransaction = notifData.idTransaction;
            result.responseCode = "00";
            result.responseDescription = "Success";

            _logger.LogInfo("      EXT              : " + notifData.externalReference);
            _logger.LogInfo("      AccountNumber    : " + notifData.accountNo);
            _logger.LogInfo("      Amount           : " + notifData.amount);
            _logger.LogInfo("      txndate          : " + notifData.transactionDate);
            _logger.LogInfo("      transactionCode  : " + notifData.transactionCode);

            bool skipped = false;
            string skipReason = "";
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    Settings settings = BRIUtility.briSettings;

                    #region validation
                    if (settings.DisableTaxInterest && (notifData.transactionCode == "NTAX" || notifData.transactionCode == "NINT"))
                    {
                        skipped = true;
                        skipReason = $"DISABLE TAX INTEREST : {notifData.transactionDescription}";
                    }

                    if (notifData.transactionCode != "NTRF" && notifData.transactionCode != "NTAX" && notifData.transactionCode != "NINT" && notifData.transactionCode != "NREV")
                    {
                        skipped = true;
                    }

                    if (settings.DepositOnly && (notifData.transactionCode != "NTRF" || notifData.transactionCode == "NREV"))
                    {
                        skipped = true;
                        skipReason = $"DEPOSIT ONLY : {notifData.transactionCode} {notifData.externalReference}";
                    }

                    if (notifData.transactionDescription.Contains(UtilityClass.ExcludeString))
                    {
                        skipped = true;
                        skipReason = $" EXCLUDE STRING {UtilityClass.ExcludeString}";
                    }

                    if (notifData.transactionCode == "NTRF")
                    {
                        if (notifData.transactionPosition == "D" || notifData.accountDebit == "020601005396305" || notifData.accountCredit == "020601005396305")
                        {
                            skipped = true;
                            skipReason = $"{notifData.transactionCode} CREDIT/DEBIT FROM OD {notifData.externalReference}";
                        }
                    }

                    if (notifData.transactionCode == "NREV")
                    {
                        if (notifData.accountDebit == "020601005396305" || notifData.accountCredit == "020601005396305")
                        {
                            skipped = true;
                            skipReason = $"{notifData.transactionCode} REVERSAL CREDIT/DEBIT FROM OD {notifData.externalReference}";
                        }
                    }


                    if (!skipped)
                    {
                        var tmpAccStatement = await connectionH2H.QueryAsync("SELECT * FROM [BRI_AccountStatement] WHERE ExternalReference = @externalReference", new { ExternalReference = notifData.externalReference });
                        if (tmpAccStatement.Count() > 0)
                        {
                            skipped = true;
                            skipReason = $" EXTERNAL REF ALREADY EXIST {notifData.externalReference}";
                        }
                        else
                        {
                            var tmpClient = await connectionBO.QueryAsync("SELECT * FROM [Client] WHERE SavingsID = @SavingsID", new { SavingsID = notifData.accountNo });
                            if (tmpClient.Count() <= 0)
                            {
                                skipped = true;
                                skipReason = $" CLIENT NOT FOUND {notifData.accountNo}";
                            }
                        }
                    }
                    #endregion

                    string query = $@" INSERT INTO [dbo].[BRI_AccountStatement]
                                    ([Amount],[OpenBalance],[CloseBalance],[TransactionDate],[ExternalReference],[Seq],[AccountNo],[AccountCurrency],[TransactionPosition]
                                    ,[TransactionCode],[AccountDebit],[AccountCredit],[TransactionDescription],[IdTransaction],[SID],[SRE],[Status],[StatusReason],[inProc],[ReceiveTime])
                                    VALUES
                                    (@Amount,@OpenBalance,@CloseBalance,@TransactionDate,@ExternalReference,@Seq,@AccountNo,@AccountCurrency,@TransactionPosition
                                    ,@TransactionCode,@AccountDebit,@AccountCredit,@TransactionDescription,@IdTransaction,@SID,@SRE,@Status,@StatusReason,@inProc,@ReceiveTime) SELECT scope_identity()";

                    BRI_AccountStatement obj = new BRI_AccountStatement(notifData);
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
                    _logger.LogInfo($"   {obj.ExternalReference} inserted ");

                    if (!skipped)
                    {
                        Thread threadobj = new Thread(new ParameterizedThreadStart(InjectBO));
                        threadobj.Start(obj);
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"InvestorAccountStatement EX: {ex.Message}");
            }
            return result;
        }
        public async Task<InvestorAccountStatementResponse> ManualPosting(int statementid)
        {
            _logger.LogInfo($"");
            _logger.LogInfo($"--------------------MANUAL POSTING-------------------------");
            InvestorAccountStatementResponse result = new InvestorAccountStatementResponse();
            result.responseCode = "0";
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {

                    string query = $@"SELECT * FROM [BRI_AccountStatement] WHERE Statement_ID = @Statement_ID";
                    BRI_AccountStatement obj = (await connectionH2H.QueryAsync<BRI_AccountStatement>(query, new { Statement_ID = statementid })).SingleOrDefault();

                    var tmpFundInOut = await connectionBO.QueryAsync("SELECT * FROM [FundInOut] WHERE Revised = 0 AND Rejected = 0 AND DocumentRef = @DocumentRef", new { DocumentRef = obj.ExternalReference });
                    if (tmpFundInOut.Count() > 0)
                    {
                        _logger.LogInfo($"  CANCEL MANUAL POSTING, ExternalRef ALREADY EXISTS " + obj.ExternalReference);
                        result.responseCode = "1";
                        return result;
                    }

                    _logger.LogInfo("      AccountNumber   : " + obj.AccountNo);
                    _logger.LogInfo("      Amount   : " + obj.Amount);
                    _logger.LogInfo("      txndate    : " + obj.TransactionDate);
                    _logger.LogInfo("      Extref    : " + obj.ExternalReference);

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
                BRI_AccountStatement obj = (BRI_AccountStatement)data;

                
                int year = Convert.ToInt32(obj.TransactionDate.Substring(0, 4));
                int month = Convert.ToInt32(obj.TransactionDate.Substring(5, 2));
                int day = Convert.ToInt32(obj.TransactionDate.Substring(8, 2));

                int hour = Convert.ToInt32(obj.TransactionDate.Substring(11, 2));
                int min = Convert.ToInt32(obj.TransactionDate.Substring(14, 2));
                int sec = Convert.ToInt32(obj.TransactionDate.Substring(17, 2));

                DateTime bcadate = new DateTime(year, month, day, hour, min, sec);
                H2HInject.Request fundin = new H2HInject.Request();
                fundin.DC = obj.TransactionPosition;
                fundin.BankNID = 1;
                fundin.BankName = "PT. BANK RAKYAT INDONESIA";
                fundin.accountNumber = obj.AccountNo;
                fundin.amount = obj.Amount;
                fundin.ExtRef = obj.ExternalReference;
                fundin.TrxType = obj.TransactionCode;
                fundin.transactionDate = bcadate;
                string query = string.Empty;

                H2HInject.Response response = await _genericService.InsertFundInOut(fundin);
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    if (response.result)
                    {
                        query = $@"UPDATE [BCA_AccountStatement]
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
                        query = $@"UPDATE [BCA_AccountStatement]
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
            Settings settings = BRIUtility.briSettings;

            string Timestamp = UtilityClass.GetTimestamp();
            string stringToSign = BRIUtility.briSettings.clientid + "|" + Timestamp;
            string signature = BRIUtility.GetSignature(stringToSign);

            try
            {
                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("X-TIMESTAMP", Timestamp);
                client.AddDefaultHeader("X-CLIENT-KEY", BRIUtility.briSettings.clientid);
                client.AddDefaultHeader("X-SIGNATURE", signature);
                client.Options.DisableCharset = true;

                objReq.grantType = "client_credentials";

                string RequestBody = JsonConvert.SerializeObject(objReq);

                var Request = new RestRequest();
                Request.Method = Method.Post;
                Request.Resource = "/snap/v1.0/access-token/b2b";
                Request.AddJsonBody(RequestBody);


                RestResponse response = client.Execute(Request);
                string body = response.Content;
                result = JsonConvert.DeserializeObject<Token>(body);


            }
            catch (Exception ex)
            {
                _logger.LogError(" ******** EXC getTokenBRI : " + ex.Message);
            }

            return result;
        }
        // BI-Fast Account Information
        // Endpoint ini digunakan untuk melakukan inquiry informasi rekening bank lain via switching Bank Indonesia (BI-Fast) yang akan digunakan sebagai rekening tujuan BI-Fast Transfer
        public async Task<InquiryBifastResponse> InquiryBifast(int BankInstructionNID, string AccountNo, string BankCode)
        {
            _logger.LogInfo("START INQUIRY BIFAST " + BankInstructionNID);
            _logger.LogInfo("==========================");
            InquiryBifastResponse result = new InquiryBifastResponse();
            InquiryBifastRequest req = new InquiryBifastRequest();
            Settings settings = BRIUtility.briSettings;
            string ServiceCode = "INQUIRY BIFAST";

            RestResponse response = new RestResponse();

            string Timestamp = UtilityClass.GetTimestamp();
            RandomString Random = await UtilityClass.GetRandomString();
            Token Token = await GetToken();

            try
            {
                req.beneficiaryAccountNo = AccountNo;
                req.beneficiaryBankCode = BankCode;

                string RequestBody = JsonConvert.SerializeObject(req);
                string payload = RequestBody.Replace("\r", "");
                payload = payload.Replace("\n", "");
                payload = payload.Replace("\t", "");

                response = await sendToBank(Method.Post, "/v1.0/transfer-bifast/inquiry-bifast", req, Timestamp, Random.ExternalID, ServiceCode);
                result = JsonConvert.DeserializeObject<InquiryBifastResponse>(response.Content);

            }
            catch (Exception ex)
            {
                _logger.LogError(" ******** EXC getInquiryBifast : " + ex.Message);
            }
            _logger.LogInfo("END INQUIRY BIFAST " + BankInstructionNID);
            _logger.LogInfo("==========================");

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

                    var queryH2H = "SELECT BankInstructionNID, BatchID, HTTPCode FROM [dbo].[BRI_OnlineTransfer] WHERE BankInstructionNID = @BankInstructionNID";

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
                    BRI_OnlineTransfer objbank = new BRI_OnlineTransfer();

                    string Timestamp = UtilityClass.GetTimestamp();
                    RandomString Random = await UtilityClass.GetRandomString();
                    string ServiceCode = "";

                    if (BankInstruction.TargetBankAccountID == BankInstruction.SourceBankAccountID)
                    {
                        ServiceCode = "TRANSFER INTRA BANK";

                        TransferIntraBankRequest req = new TransferIntraBankRequest();
                        req.partnerReferenceNo = Random.PartnerReferenceNo;
                        req.amount.value = BankInstruction.Amount.ToString("0.00");
                        req.amount.currency = "IDR";
                        req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;
                        req.customerReference = Random.CustomerReference;
                        req.feeType = "OUR";
                        req.remark = UtilityClass.ExcludeString + "-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                        req.sourceAccountNo = BankInstruction.SourceSavingsID;
                        req.transactionDate = Timestamp;
                        req.additionalInfo.deviceId = "";
                        req.additionalInfo.channel = "";
                        req.additionalInfo.isRdn = "true";

                        response = await sendToBank(Method.Post, "/intrabank/snap/v1.0/transfer-intrabank", req, Timestamp, Random.ExternalID, ServiceCode);
                        result = JsonConvert.DeserializeObject<TransferIntraBankResponse>(response.Content);

                        objbank.BankInstructionNID = BankInstructionNID;
                        objbank.BatchID = 0;
                        objbank.ClientNID = Client.ClientNID;
                        objbank.ClientID = BankInstruction.ClientID;
                        objbank.Amount = req.amount.value;
                        objbank.SourceAccountNo = req.sourceAccountNo;
                        objbank.SourceAccountName = BankInstruction.SourceSavingsName;
                        objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                        objbank.BeneficiaryAccountName = BankInstruction.TargetSavingsName;
                        objbank.Type = BankInstruction.Type;
                        objbank.SendTime = DateTime.Now;
                        objbank.TransactionDate = req.transactionDate;
                        objbank.ReceivedTime = DateTime.Now;                       
                        objbank.ServiceCode = ServiceCode;
                        objbank.PartnerReferenceNo = req.partnerReferenceNo;
                        objbank.CustomerReference = req.customerReference;
                        objbank.DeviceId = req.additionalInfo.deviceId;
                        objbank.Channel = req.additionalInfo.channel;
                        objbank.IsRdn = req.additionalInfo.isRdn;
                        objbank.BeneficiaryBankCode = "";
                        objbank.Remark = req.remark;
                        objbank.PaymentInfo = "";
                        objbank.SenderType = "";
                        objbank.SenderResidentStatus = "";
                        objbank.SenderTownName = "";
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

                            BIfastRequest req = new BIfastRequest();                            

                            req.customerReference = Random.CustomerReference;
                            req.senderIdentityNumber = "3515085211930002";
                            req.sourceAccountNo = BankInstruction.SourceSavingsID;
                            req.amount.value = BankInstruction.Amount.ToString("0.00");
                            req.amount.currency = "IDR";
                            req.beneficiaryBankCode = targetBankCode.BICCode;
                            req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;

                            InquiryBifastResponse Account = await InquiryBifast(BankInstructionNID, req.beneficiaryAccountNo, req.beneficiaryBankCode);

                            req.referenceNo = Account.referenceNo;
                            req.externalId = Account.externalId;
                            req.transactionDate = Timestamp;
                            req.paymentInfo = UtilityClass.ExcludeString + "-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                            req.senderType = "01";
                            req.senderResidentStatus = "01";
                            req.senderTownName = "0300";
                            req.additionalInfo.deviceId = "12345679237";
                            req.additionalInfo.channel = "mobilephone";
                            req.additionalInfo.isRdn = "true";

                            response = await sendToBank(Method.Post, "/v1.0/transfer-bifast/payment-bifast", req, Timestamp, Random.ExternalID, ServiceCode);
                            result = JsonConvert.DeserializeObject<BIfastResponse>(response.Content);

                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = 0;
                            objbank.ClientNID = Client.ClientNID;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.Amount = req.amount.value;
                            objbank.SourceAccountNo = req.sourceAccountNo;
                            objbank.SourceAccountName = BankInstruction.SourceSavingsName;
                            objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                            objbank.BeneficiaryAccountName = BankInstruction.TargetSavingsName;
                            objbank.Type = BankInstruction.Type;                            
                            objbank.TransactionDate = req.transactionDate;
                            objbank.SendTime = DateTime.Now;
                            objbank.ReceivedTime = DateTime.Now;                            
                            objbank.ServiceCode = ServiceCode; ;
                            objbank.PartnerReferenceNo = "";
                            objbank.CustomerReference = req.customerReference;
                            objbank.DeviceId = req.additionalInfo.deviceId;
                            objbank.Channel = req.additionalInfo.channel;
                            objbank.IsRdn = req.additionalInfo.isRdn;
                            objbank.BeneficiaryBankCode = req.beneficiaryBankCode;
                            objbank.PaymentInfo = req.paymentInfo;
                            objbank.SenderType = req.senderType;
                            objbank.SenderResidentStatus = req.senderResidentStatus;
                            objbank.SenderTownName = req.senderTownName;
                            objbank.HTTPCode = response.StatusCode.ToString();
                            objbank.StatusCode = result.responseCode;
                            objbank.StatusMessage = result.responseMessage;

                        }

                    }

                    string query = @"INSERT INTO [dbo].[BRI_OnlineTransfer]
                                        ([BankInstructionNID],[BatchID],[ClientNID],[ClientID],[Amount],[SourceAccountNo],[SourceAccountName],[BeneficiaryAccountNo]
                                        ,[BeneficiaryAccountName],[Type],[SendTime],[TransactionDate],[ReceivedTime],[ServiceCode]
                                        ,[PartnerReferenceNo],[CustomerReference],[DeviceId],[Channel],[IsRdn],[BeneficiaryBankCode],[PaymentInfo],[SenderType]
                                        ,[SenderResidentStatus],[SenderTownName],[HTTPCode],[StatusCode],[StatusMessage])
                                         VALUES
                                        (@BankInstructionNID,@BatchID,@ClientNID,@ClientID,@Amount,@SourceAccountNo,@SourceAccountName,@BeneficiaryAccountNo
                                        ,@BeneficiaryAccountName,@Type,@SendTime,@TransactionDate,@ReceivedTime,@ServiceCode
                                        ,@PartnerReferenceNo,@CustomerReference,@DeviceId,@Channel,@IsRdn,@BeneficiaryBankCode,@PaymentInfo,@SenderType
                                        ,@SenderResidentStatus,@SenderTownName,@HTTPCode,@StatusCode,@StatusMessage) SELECT scope_identity()";

                    objbank.AutoNID = await connectionH2H.ExecuteScalarAsync<long>(query, objbank);
                    _logger.LogInfo("         [BRI_OnlineTransfer] INSERTED " + objbank.AutoNID);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        query = "UPDATE [BRI_OnlineTransfer] SET BatchID = @BatchID WHERE BankInstructionNID = @BankInstructionNID AND AutoNID = @AutoNID";
                        var ph2h = new DynamicParameters();
                        ph2h.Add("@AutoNID", objbank.AutoNID);
                        ph2h.Add("@BatchID", BatchID);
                        ph2h.Add("@BankInstructionNID", BankInstructionNID);
                        await connectionH2H.ExecuteAsync(query, ph2h);

                        _logger.LogInfo("         [BRI_OnlineTransfer] UPDATE " + objbank.AutoNID);


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
            _logger.LogInfo("SEND TO BANK BRI");
            _logger.LogInfo("==========================");
            _logger.LogInfo($"SERVICE {servicecode}");
            try
            {

                string RequestBody = JsonConvert.SerializeObject(body);
                string payload = RequestBody.Replace("\r", "");
                payload = payload.Replace("\n", "");
                payload = payload.Replace("\t", "");

                Token BRIToken = await GetToken();

                Settings settings = BRIUtility.briSettings;

                string StringtoSign = await BRIUtility.StringtoSign(method.ToString().ToUpper(), url, BRIToken.accessToken, payload, timestamp);
                string SignatureService = await BRIUtility.GetSignatureService(settings.clientsecret, StringtoSign);


                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("Authorization", "Bearer " + BRIToken.accessToken);
                client.AddDefaultHeader("X-TIMESTAMP", timestamp);
                client.AddDefaultHeader("X-SIGNATURE", SignatureService);
                client.AddDefaultHeader("X-PARTNER-ID", "UATCORP001");
                client.AddDefaultHeader("X-EXTERNAL-ID", randomExternal);
                client.AddDefaultHeader("CHANNEL-ID", "865");
                client.Options.DisableCharset = true;

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
                _logger.LogError(" **** EXC SEND TO BANK BRI : " + ex.Message);
            }

            return result;
        }
        #endregion
    }



}
