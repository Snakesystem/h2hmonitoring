using Dapper;
using H2HAPICore.Context;
using H2HAPICore.Model.BCA;
using H2HAPICore.Model.S21BO;
using Newtonsoft.Json;
using NLog;
using RestSharp;
using System;
using System.Dynamic;
using System.Net;
using System.Security.Cryptography;
using System.Text;



namespace H2HAPICore.Services
{
    public interface IBCAService
    {
        public Task<CreateToken> CreateToken();
        public Task<InvestorAccountStatementResponse> InvestorAccountStatement(InvestorAccountStatementRequest notifData);
        public Task<InvestorAccountStatementResponse> ManualPosting(int statementid);
        public Task<Token> GetToken();
        public Task<object> OnlineTransfer(int BankInstructionNID, bool bypass);
        public Task<string> generateSignature(string method, string token, string url, string api_key, string apisecret, string body, string timestamp);
        public Task<object> ValidationAccount(string accountnumber, string firstname);
        public Task<TokenBCA> GetTokenRDN();
        public Task<object> GetDataAccountStatement();
    }

    public class BCAService : IBCAService
    {
        private readonly DapperContext _context;
        private readonly ILoggerManager _logger;
        private readonly IGenericService _genericService;

        public BCAService(DapperContext context, ILoggerManager logger, IGenericService genericService)
        {
            _context = context;
            _logger = logger;
            _genericService = genericService;
        }

        #region RDN
        public async Task<TokenBCA> GetTokenRDN()
        {
            
            TokenBCA result = new TokenBCA();
            _logger.LogInfo("");
            _logger.LogInfo("START GetTokenRDN ");
            _logger.LogInfo("==========================");

            Settings settings = BCAUtility.bcaSettings;

            var client = new RestClient(settings.URL);
            client.AddDefaultHeader("Authorization", "Basic " + UtilityClass.Base64Encode(settings.RDN.clientid + ":" + settings.RDN.clientsecret));
            client.AddDefaultHeader("Content-Type", "application/x-www-form-urlencoded");


            var Request = new RestRequest();
            Request.Method = Method.Post;
            Request.Resource = settings.URL + "/api/oauth/token";
            Request.AddParameter("grant_type", "client_credentials");

            RestResponse response = client.Execute(Request);
            string body = response.Content;
            _logger.LogInfo($"         [Response] {response.Content}");
            _logger.LogInfo("==========================");

            result = JsonConvert.DeserializeObject<TokenBCA>(body);           

            return result;

        }

        public async Task<object> ValidationAccount(string accountnumber, string firstname)
        {
            dynamic result = new ExpandoObject();
            _logger.LogInfo("");
            _logger.LogInfo("START Validation Account " + accountnumber);
            _logger.LogInfo("==========================");

            Settings settings = BCAUtility.bcaSettings;

            TokenBCA token = await GetTokenRDN();

            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "+07:00";

            string relativeURL = "/banking/general/corporates/KBBPUSEKUR/accounts/" + accountnumber + "/validation?Action=validate&By=name&Value=" + firstname;
            string delimiter = ":";
            string strToSign = "GET" + delimiter + relativeURL + delimiter + token.access_token + delimiter + UtilityClass.SHA256HexHashString("") + delimiter + timestamp;

            HMACSHA256 hmac = new HMACSHA256(Encoding.ASCII.GetBytes(settings.RDN.apisecret));
            byte[] hmac_hashByte = hmac.ComputeHash(Encoding.ASCII.GetBytes(strToSign));
            string Signature = string.Join("", hmac_hashByte.ToList().Select(b => b.ToString("x2")).ToArray());

            var client = new RestClient(settings.URL);
            client.AddDefaultHeader("Authorization", "Bearer " + token.access_token);
            client.AddDefaultHeader("Content-Type", "application/json");
            client.AddDefaultHeader("Origin", "https://www.yuanta.co.id");
            client.AddDefaultHeader("X-BCA-Key", settings.RDN.apikey);
            client.AddDefaultHeader("X-BCA-Timestamp", timestamp);
            client.AddDefaultHeader("X-BCA-Signature", Signature);

            _logger.LogInfo($"Authorization     Bearer {token.access_token}");
            _logger.LogInfo($"Content-Type      application/json");
            _logger.LogInfo($"Origin            https://www.yuanta.co.id");
            _logger.LogInfo($"X-BCA-Key         {settings.RDN.apikey}");
            _logger.LogInfo($"X-BCA-Timestamp   {timestamp}");
            _logger.LogInfo($"X-BCA-Signature   {Signature}");

            _logger.LogInfo($"URL               {settings.URL}/banking/general/corporates/KBBPUSEKUR/accounts/{accountnumber}/validation?Action=validate&By=name&Value={firstname}");

            var Request = new RestRequest();
            Request.Method = Method.Get;
            Request.Resource = settings.URL+ "/banking/general/corporates/KBBPUSEKUR/accounts/" + accountnumber + "/validation";
            Request.AddParameter("Action", "validate");
            Request.AddParameter("By", "name");
            Request.AddParameter("Value", firstname);

            RestResponse response = client.Execute(Request);
            _logger.LogInfo($"         [Response] {response.Content}");
            _logger.LogInfo("==========================");

            return response;

        }
        #endregion

        #region Notification Deposit
        public async Task<CreateToken> CreateToken()
        {
            _logger.LogInfo($"CreateToken");
            CreateToken token = new CreateToken();
            try
            {
                token.access_token = UtilityClass.createToken();
                token.expires_in = BCAUtility.bcaSettings.ExpireToken;
                token.scope = "resource.WRITE resource.READ";
                token.token_type = "bearer";
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetToken EX: {ex.Message}");
            }
            return token;
        }
        public async Task<InvestorAccountStatementResponse> InvestorAccountStatement(InvestorAccountStatementRequest notifData)
        {
            _logger.LogInfo($"");
            _logger.LogInfo($"");
            _logger.LogInfo($"InvestorAccountStatement");
            InvestorAccountStatementResponse result = new InvestorAccountStatementResponse();
            result.ResponseWS = "0";

            _logger.LogInfo("      AccountNumber   : " + notifData.AccountNumber);
            _logger.LogInfo("      Amount   : " + notifData.TxnAmount);
            _logger.LogInfo("      txndate    : " + notifData.TxnDate);
            _logger.LogInfo("      txntype    : " + notifData.TxnType);

            bool skipped = false;
            string skipReason = "";
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    Settings settings = BCAUtility.bcaSettings;

                    #region validation
                    if (settings.DisableTaxInterest && (notifData.TxnType == "NTAX" || notifData.TxnType == "NINT"))
                    {
                        skipped = true;
                        skipReason = $"DISABLE TAX INTEREST : {notifData.TxnType}";
                    }

                    if (notifData.TxnType != "NTRF" && notifData.TxnType != "NTAX" && notifData.TxnType != "NINT" && notifData.TxnType != "NCHG" && notifData.TxnType != "NREV" && notifData.TxnType != "NKOR")
                    {
                        skipped = true;
                        skipReason = $"TESTING : {notifData.TxnType}";
                    }

                    if (settings.DepositOnly && (notifData.TxnType != "NTRF" || notifData.TxnCode == "D"))
                    {
                        skipped = true;
                        skipReason = $"DEPOSIT ONLY : {notifData.TxnType} {notifData.TxnCode}";
                    }

                    if (notifData.TxnDesc.Contains(settings.ExcludeString))
                    {
                        skipped = true;
                        skipReason = $" EXCLUDE STRING {settings.ExcludeString}";
                    }

                    if (!skipped)
                    {
                        var tmpAccStatement = await connectionH2H.QueryAsync("SELECT * FROM [BCA_AccountStatement] WHERE ExternalRef = @ExternalRef", new { ExternalRef = notifData.ExternalReference });
                        if (tmpAccStatement.Count() > 0)
                        {
                            skipped = true;
                            skipReason = $" EXTERNAL REF ALREADY EXIST {notifData.ExternalReference}";
                        }
                        else
                        {
                            var tmpClient = await connectionBO.QueryAsync("SELECT * FROM [Client] WHERE SavingsID = @SavingsID", new { SavingsID = notifData.AccountNumber });
                            if (tmpClient.Count() <= 0)
                            {
                                skipped = true;
                                skipReason = $" CLIENT NOT FOUND {notifData.AccountNumber}";
                            }
                        }
                    }
                    #endregion

                    string query = $@" INSERT INTO [dbo].[BCA_AccountStatement]
                                    ([TxnAmount],[OpenBalance],[CloseBalance],[TxnDate],[ExternalRef],[SeqNumber],[AccountNumber],[Currency],[TxnType]
                                    ,[TxnCode],[AccountDebit],[AccountCredit],[TxnDesc],[Status],[StatusReason],[inProc],[ReceiveTime])
                                    VALUES
                                    (@TxnAmount,@OpenBalance,@CloseBalance,@TxnDate,@ExternalRef,@SeqNumber,@AccountNumber,@Currency
                                    ,@TxnType,@TxnCode,@AccountDebit,@AccountCredit,@TxnDesc,@Status,@StatusReason,@inProc,@ReceiveTime) SELECT scope_identity()";

                    BCA_AccountStatement obj = new BCA_AccountStatement(notifData);
                    obj.inProc = true;
                    obj.Status = 1;
                    if (skipped)
                    {
                        obj.Status = 2;
                        obj.inProc = false;
                        obj.StatusReason = skipReason;
                    }
                    obj.ReceiveTime = DateTime.Now;

                    result.ResponseWS = "0";
                    obj.Statement_ID = await connectionH2H.ExecuteScalarAsync<long>(query, obj);
                    _logger.LogInfo($"   {obj.ExternalRef} inserted ");

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
            result.ResponseWS = "0";
            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {

                    string query = $@"SELECT * FROM [BCA_AccountStatement] WHERE Statement_ID = @Statement_ID";
                    BCA_AccountStatement obj = (await connectionH2H.QueryAsync<BCA_AccountStatement>(query, new { Statement_ID = statementid })).SingleOrDefault();

                    var tmpFundInOut = await connectionBO.QueryAsync("SELECT * FROM [FundInOut] WHERE Revised = 0 AND Rejected = 0 AND DocumentRef = @DocumentRef", new { DocumentRef = obj.ExternalRef });
                    if (tmpFundInOut.Count() > 0)
                    {
                        _logger.LogInfo($"  CANCEL MANUAL POSTING, ExternalRef ALREADY EXISTS " + obj.ExternalRef);
                        result.ResponseWS = "1";
                        return result;
                    }

                    _logger.LogInfo("      AccountNumber   : " + obj.AccountNumber);
                    _logger.LogInfo("      Amount   : " + obj.TxnAmount);
                    _logger.LogInfo("      txndate    : " + obj.TxnDate);
                    _logger.LogInfo("      Extref    : " + obj.ExternalRef);

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
                BCA_AccountStatement obj = (BCA_AccountStatement)data;

                int month = Convert.ToInt32(obj.TxnDate.Substring(0, 2));
                int day = Convert.ToInt32(obj.TxnDate.Substring(2, 2));
                int year = Convert.ToInt32(obj.TxnDate.Substring(4, 4));
                int hour = Convert.ToInt32(obj.TxnDate.Substring(9, 2));
                int min = Convert.ToInt32(obj.TxnDate.Substring(11, 2));
                int sec = Convert.ToInt32(obj.TxnDate.Substring(13, 2));

                DateTime bcadate = new DateTime(year, month, day, hour, min, sec);
                H2HInject.Request fundin = new H2HInject.Request();
                fundin.DC = obj.TxnCode;
                fundin.BankNID = 6;
                fundin.BankName = "PT. BANK CENTRAL ASIA, Tbk.";
                fundin.accountNumber = obj.AccountNumber;
                fundin.amount = obj.TxnAmount;
                fundin.ExtRef = obj.ExternalRef;
                fundin.TrxType = obj.TxnType;
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
        public async Task<string> generateSignature(string method, string token, string url, string api_key, string apisecret, string body, string timestamp)
        {
            string result = "";
            string delimiter = ":";
            try
            {
                body = body.Replace("\r", "");
                body = body.Replace("\n", "");
                body = body.Replace(" ", "");

                string strToSign = method.ToUpper() + delimiter + url + delimiter + token + delimiter + UtilityClass.SHA256HexHashString(body) + delimiter + timestamp;
                HMACSHA256 hmac = new HMACSHA256(Encoding.ASCII.GetBytes(apisecret));
                byte[] hmac_hashByte = hmac.ComputeHash(Encoding.ASCII.GetBytes(strToSign));
                return string.Join("", hmac_hashByte.ToList().Select(b => b.ToString("x2")).ToArray());
            }
            catch (Exception ex)
            {
                Program.logger.Info(" **** EXC getBCAHMAC  : " + ex.Message);
                result = "FAILED";
            }
            Program.logger.Info("            validateSign result : " + result);
            return result;
        }
        #endregion

        #region Withdrawal Instruction
        public async Task<Token> GetToken()
        {
            _logger.LogInfo("");
            _logger.LogInfo("GET TOKEN");
            Token result = new Token();
            TokenRequest objReq = new TokenRequest();
            Settings settings = BCAUtility.bcaSettings;

            string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "+07:00";
            string stringToSign = BCAUtility.bcaSettings.OutBound.clientid + "|" + timestamp;
            string signature = BCAUtility.GetSignature(stringToSign);

            try
            {

                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("X-TIMESTAMP", timestamp);
                client.AddDefaultHeader("X-CLIENT-KEY", BCAUtility.bcaSettings.OutBound.clientid);
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
                _logger.LogError(" ******** EXC GetToken : " + ex.Message);
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

                    var queryH2H = "SELECT BankInstructionNID, BatchID, HTTPCode FROM [dbo].[BCA_OnlineTransfer] WHERE BankInstructionNID = @BankInstructionNID";

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
                    BCA_OnlineTransfer objbank = new BCA_OnlineTransfer();

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
                        req.feeType = "OUR";
                        req.remark = UtilityClass.ExcludeString + "-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                        req.sourceAccountNo = BankInstruction.SourceSavingsID;
                        req.transactionDate = Timestamp;
                        req.additionalInfo.economicActivity = "";
                        req.additionalInfo.transactionPurpose = "";

                        objbank.BankInstructionNID = BankInstructionNID;
                        objbank.BatchID = 0;
                        objbank.ClientID = BankInstruction.ClientID;
                        objbank.ClientNID = Client.ClientNID;
                        objbank.Amount = req.amount.value;
                        objbank.SourceAccountNo = req.sourceAccountNo;
                        objbank.SourceAccountName = BankInstruction.SourceSavingsName;
                        objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                        objbank.BeneficiaryAccountName = BankInstruction.TargetSavingsName;
                        objbank.Type = BankInstruction.Type;
                        objbank.TransactionDate = req.transactionDate;
                        objbank.SendTime = DateTime.Now;

                        response = await sendToBank(Method.Post, "/openapi/v1.0/transfer-intrabank", req, Timestamp, Random.ExternalID, ServiceCode);
                        result = JsonConvert.DeserializeObject<TransferIntraBankResponse>(response.Content);                        
                        
                        objbank.ReceivedTime = DateTime.Now;                        
                        objbank.ServiceCode = ServiceCode;
                        objbank.PartnerReferenceNo = req.partnerReferenceNo;
                        objbank.CustomerReference = req.customerReference;
                        objbank.FeeType = "";
                        objbank.Remark = "";
                        objbank.EconomicActivity = "";
                        objbank.TransactionPurpose = "";
                        objbank.BeneficiaryEmail = req.beneficiaryEmail;
                        objbank.BeneficiaryBankName = "";
                        objbank.BeneficiaryBankCode = "";
                        objbank.BeneficiaryAddress = "";
                        objbank.TransferType = "";
                        objbank.PurposeCode = "";
                        objbank.BeneficiaryCustomerResidence = "";
                        objbank.BeneficiaryCustomerType = "";
                        objbank.KodePos = "";
                        objbank.ReceiverPhone = "";
                        objbank.SenderCustomerResidence = "";
                        objbank.SenderCustomerType = "";
                        objbank.SenderPhone = "";
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

                            BIFastRequest req = new BIFastRequest();

                            req.partnerReferenceNo = Random.PartnerReferenceNo;
                            req.amount.value = BankInstruction.Amount.ToString("0.00");
                            req.amount.currency = "IDR";
                            req.beneficiaryAccountName = BankInstruction.TargetSavingsName;
                            req.beneficiaryAccountNo = BankInstruction.TargetSavingsID;
                            req.beneficiaryBankCode = targetBankCode.BICCode;
                            req.beneficiaryEmail = Client.Email;
                            req.currency = "IDR";
                            if (!string.IsNullOrEmpty(Client.Email))
                            {
                                string[] emails = Client.Email.Split(";".ToCharArray());
                                if (emails.Count() > 0)
                                {
                                    req.beneficiaryEmail = emails[0];
                                }
                            }
                            req.sourceAccountNo = BankInstruction.SourceSavingsID;
                            req.transactionDate = Timestamp;
                            req.additionalInfo.transferType = "2";
                            req.additionalInfo.purposeCode = "02";                            

                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = 0;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.ClientNID = Client.ClientNID;
                            objbank.Amount = req.amount.value;
                            objbank.SourceAccountNo = req.sourceAccountNo;
                            objbank.SourceAccountName = BankInstruction.SourceSavingsName;
                            objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                            objbank.BeneficiaryAccountName = BankInstruction.TargetSavingsName;
                            objbank.Type = BankInstruction.Type;                            
                            objbank.TransactionDate = req.transactionDate;
                            objbank.SendTime = DateTime.Now;

                            response = await sendToBank(Method.Post, "/openapi/v2.0/transfer-interbank", req, Timestamp, Random.ExternalID, ServiceCode);
                            result = JsonConvert.DeserializeObject<TransferIntraBankResponse>(response.Content);

                            objbank.ReceivedTime = DateTime.Now;
                            objbank.HTTPCode = response.StatusCode.ToString();
                            objbank.StatusCode = result.responseCode;
                            objbank.StatusMessage = result.responseMessage;
                            objbank.ServiceCode = ServiceCode;
                            objbank.PartnerReferenceNo = req.partnerReferenceNo;
                            objbank.CustomerReference = "";
                            objbank.FeeType = "";
                            objbank.Remark = "";
                            objbank.EconomicActivity = "";
                            objbank.TransactionPurpose = "";
                            objbank.BeneficiaryEmail = req.beneficiaryEmail;
                            objbank.BeneficiaryBankName = "";
                            objbank.BeneficiaryBankCode = "";
                            objbank.BeneficiaryAddress = "";
                            objbank.TransferType = req.additionalInfo.transferType;
                            objbank.PurposeCode = req.additionalInfo.purposeCode;
                            objbank.BeneficiaryCustomerResidence = "";
                            objbank.BeneficiaryCustomerType = "";
                            objbank.KodePos = "";
                            objbank.ReceiverPhone = "";
                            objbank.SenderCustomerResidence = "";
                            objbank.SenderCustomerType = "";
                            objbank.SenderPhone = "";

                        }
                        else
                        {
                            LLGRTGSRequest req = new LLGRTGSRequest();

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
                            req.beneficiaryBankCode = targetBankCode.BICCode;
                            req.beneficiaryBankName = targetBankCode.BankName;
                            req.beneficiaryCustomerResidence = "1";
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
                            req.customerReference = Random.CustomerReference;
                            req.feeType = "OUR";
                            req.kodepos = "";
                            req.receiverPhone = "";
                            req.remark = "$S21$-" + BankInstruction.ClientID + "-" + BankInstructionNID;
                            req.senderCustomerResidence = "";
                            req.senderCustomerType = "";
                            req.senderPhone = "";
                            req.sourceAccountNo = BankInstruction.SourceSavingsID;
                            req.transactionDate = Timestamp;

                            if (BankInstruction.Amount < 100000000)
                            {
                                ServiceCode = "LLG";
                                URL = "/openapi/v1.0/transfer-skn";
                            }
                            else
                            {
                                ServiceCode = "RTGS";
                                URL = "/openapi/v1.0/transfer-rtgs";
                            }

                           
                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = 0;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.ClientNID = Client.ClientNID;
                            objbank.Amount = req.amount.value;
                            objbank.SourceAccountNo = req.sourceAccountNo;
                            objbank.SourceAccountName = BankInstruction.SourceSavingsName;
                            objbank.BeneficiaryAccountNo = req.beneficiaryAccountNo;
                            objbank.BeneficiaryAccountName = BankInstruction.TargetSavingsName;
                            objbank.Type = BankInstruction.Type;                            
                            objbank.TransactionDate = req.transactionDate;
                            objbank.SendTime = DateTime.Now;

                            response = await sendToBank(Method.Post, URL, req, Timestamp, Random.ExternalID, ServiceCode);
                            result = JsonConvert.DeserializeObject<LLGRTGSResponse>(response.Content);

                            objbank.ReceivedTime = DateTime.Now;
                            objbank.HTTPCode = response.StatusCode.ToString();
                            objbank.StatusCode = result.responseCode;
                            objbank.StatusMessage = result.responseMessage;
                            objbank.ServiceCode = ServiceCode;
                            objbank.PartnerReferenceNo = req.partnerReferenceNo;
                            objbank.CustomerReference = req.customerReference;
                            objbank.FeeType = req.feeType;
                            objbank.Remark = req.remark;
                            objbank.EconomicActivity = "";
                            objbank.TransactionPurpose = "";
                            objbank.BeneficiaryEmail = req.beneficiaryEmail;
                            objbank.BeneficiaryBankName = req.beneficiaryBankName;
                            objbank.BeneficiaryBankCode = req.beneficiaryBankCode;
                            objbank.BeneficiaryAddress = req.beneficiaryAddress;
                            objbank.TransferType = "";
                            objbank.PurposeCode = "";
                            objbank.BeneficiaryCustomerResidence = req.beneficiaryCustomerResidence;
                            objbank.BeneficiaryCustomerType = req.beneficiaryCustomerType;
                            objbank.KodePos = req.kodepos;
                            objbank.ReceiverPhone = req.receiverPhone;
                            objbank.SenderCustomerResidence = req.senderCustomerResidence;
                            objbank.SenderCustomerType = req.senderCustomerType;
                            objbank.SenderPhone = req.senderPhone;

                        }
                        
                    }

                    string query = @"INSERT INTO [dbo].[BCA_OnlineTransfer]
                                        ([BankInstructionNID],[BatchID],[ClientNID],[ClientID],[Amount],[SourceAccountNo],[SourceAccountName],[BeneficiaryAccountNo],[BeneficiaryAccountName]
                                        ,[Type],[TransactionDate],[SendTime],[ReceivedTime],[ServiceCode],[PartnerReferenceNo],[CustomerReference],[FeeType]
                                        ,[Remark],[EconomicActivity],[TransactionPurpose],[BeneficiaryEmail],[BeneficiaryBankName],[BeneficiaryBankCode]
                                        ,[BeneficiaryAddress],[TransferType],[PurposeCode],[BeneficiaryCustomerResidence],[BeneficiaryCustomerType],[KodePos],[ReceiverPhone]
                                        ,[SenderCustomerResidence],[SenderCustomerType],[SenderPhone],[HTTPCode],[StatusCode],[StatusMessage])
                                         VALUES
                                        (@BankInstructionNID,@BatchID,@ClientNID,@ClientID,@Amount,@SourceAccountNo,@SourceAccountName,@BeneficiaryAccountNo,@BeneficiaryAccountName
                                        ,@Type,@TransactionDate,@SendTime,@ReceivedTime,@ServiceCode,@PartnerReferenceNo,@CustomerReference,@FeeType
                                        ,@Remark,@EconomicActivity,@TransactionPurpose,@BeneficiaryEmail,@BeneficiaryBankName,@BeneficiaryBankCode                                       
                                        ,@BeneficiaryAddress,@TransferType,@PurposeCode,@BeneficiaryCustomerResidence,@BeneficiaryCustomerType,@KodePos,@ReceiverPhone
                                        ,@SenderCustomerResidence,@SenderCustomerType,@SenderPhone,@HTTPCode,@StatusCode,@StatusMessage) SELECT scope_identity()";

                    objbank.AutoNID = await connectionH2H.ExecuteScalarAsync<long>(query, objbank);
                    _logger.LogInfo("         [BCA_OnlineTransfer] INSERTED " + objbank.AutoNID);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {

                        query = "UPDATE [BCA_OnlineTransfer] SET BatchID = @BatchID WHERE BankInstructionNID = @BankInstructionNID AND AutoNID = @AutoNID";
                        var ph2h = new DynamicParameters();
                        ph2h.Add("@AutoNID", objbank.AutoNID);
                        ph2h.Add("@BatchID", BatchID);
                        ph2h.Add("@BankInstructionNID", BankInstructionNID);
                        await connectionH2H.ExecuteAsync(query, ph2h);

                        _logger.LogInfo("         [BCA_OnlineTransfer] UPDATE " + objbank.AutoNID);


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
            _logger.LogInfo("SEND TO BANK BCA");
            _logger.LogInfo("==========================");
            _logger.LogInfo($"SERVICE {servicecode}");
            try
            {

                string RequestBody = JsonConvert.SerializeObject(body);
                string payload = RequestBody.Replace("\r", "");
                payload = payload.Replace("\n", "");
                payload = payload.Replace("\t", "");

                Token bcaToken = await GetToken();

                Settings settings = BCAUtility.bcaSettings;

                string StringtoSign = await BCAUtility.StringtoSign(method.ToString().ToUpper(), url, bcaToken.accessToken, payload, timestamp);
                string SignatureService = await BCAUtility.GetSignatureService(settings.OutBound.clientsecret, StringtoSign);


                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Authorization", "Bearer " + bcaToken.accessToken);
                client.AddDefaultHeader("Content-Type", "application/json");
                client.AddDefaultHeader("X-TIMESTAMP", timestamp);
                client.AddDefaultHeader("X-SIGNATURE", SignatureService);
                client.AddDefaultHeader("X-EXTERNAL-ID", randomExternal);
                client.AddDefaultHeader("CHANNEL-ID", "95051");
                client.AddDefaultHeader("X-PARTNER-ID", settings.OutBound.PARTNERID);               

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
                _logger.LogError(" **** EXC SEND TO BANK BCA : " + ex.Message);
            }

            return result;
        }
        #endregion

        #region
        public async Task<object> GetDataAccountStatement()
        {
            dynamic result = new ExpandoObject();

            try
            {
                using (var connectionBO = _context.CreateConnectionBO())
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    string query = $@"SELECT Statement_ID, ClientID, TxnAmount As Amount, TxnDate AS TransactionDate, AccountNumber AS NomorRDN, TxnCode as TransactionCode, 
                                    CASE WHEN TxnType = 'NTRF' AND TxnCode = 'C' THEN 'DEPOSIT' 
                                    WHEN TxnType = 'NTRF' AND TxnCode = 'D' THEN 'DEBIT' 
                                    WHEN TxnType = 'NINT' THEN 'INTEREST' 
                                    WHEN TxnType = 'NREV' THEN 'CREDIT REVERSAL' 
                                    WHEN TxnType = 'NTAX' THEN 'TAX' 
                                    WHEN TxnType = 'NCHG' THEN 'ADMIN FEE' 
                                    WHEN TxnType = 'NKOR' AND TxnCode = 'D' THEN 'DEBIT CORRECTION'
                                    WHEN TxnType = 'NKOR' AND TxnCode = 'C' THEN 'CREDIT CORRECTION' END  as TransactionType, 
                                    FundInOutNID, FundNID ,CASE WHEN Status = 1 THEN 'SUCCESS' ELSE 'SKIPPED' END AS StatusBO, StatusReason as Reason, ExternalRef as ExternalReference, ReceiveTime
                                    from BCA_AccountStatement";

                    result = await connectionH2H.QueryAsync(query);


                }
            }
            catch (Exception ex)
            {
                _logger.LogError(" **** EXC GET DATA ACCOUNT STATEMENT BANK BCA : " + ex.Message);
            }

            return result;
        }

        #endregion
    }
}
