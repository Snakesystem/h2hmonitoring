using Dapper;
using H2HAPICore.Context;
using H2HAPICore.Model.S21BO;
using H2HAPICore.Model.CIMB;
using Newtonsoft.Json;
using RestSharp;
using System.Dynamic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.ComponentModel.DataAnnotations;
using System.ServiceModel;
using System.Data;
using System.Xml.Serialization;
using NLog;

namespace H2HAPICore.Services
{
    public interface ICIMBInstructionService
    {
        public void tes();
        public Task<object> OnlineTransfer(int BankInstructionNID, bool bypass);
        public Task<string> getCIMBToken(string txnRequestDateTime, string requestID, string serviceCode);
    }

    [ServiceContract(Namespace = "http://172.17.121.45")]
    public interface ICIMBDepositService
    {
        [OperationContract]
        [System.ServiceModel.XmlSerializerFormatAttribute(SupportFaults = true)]
        Task<Output> doReceivePushData([Required] Input input);
    }


    public class CIMBInstructionService : ICIMBInstructionService
    {
        private readonly DapperContext _context;
        private readonly Logger _logger;

        public CIMBInstructionService(DapperContext context, IGenericService genericService)
        {
            _context = context;
            _logger = LogManager.GetLogger("CIMBInstruksi");
        }

        public async Task<string> getCIMBToken(string txnRequestDateTime, string requestID, string serviceCode)
        {
            _logger.Info("getCIMBToken");
            string result = "";
            Settings settings = CIMBUtility.cimbSettings;
            try
            {
                string CorpID = settings.CorpID;
                string SecurityWord = settings.SecurityWord;
                string HashSW = UtilityClass.SHA256HexHashString(SecurityWord);
                string delimiter = ":";
                string toHash = CorpID + delimiter + HashSW + delimiter + txnRequestDateTime + delimiter + requestID + delimiter + serviceCode;
                string token = UtilityClass.SHA256HexHashString(toHash);

                result = token;
            }
            catch (Exception ex)
            {
                _logger.Error(" ******** EXC getBCAToken : " + ex.Message);
            }

            return result;
        }

        public async Task<object> OnlineTransfer(int BankInstructionNID, bool bypass)
        {
            dynamic result = new ExpandoObject();
            _logger.Info("");
            _logger.Info(" INHOUSE ONLINE TRANSFER " + BankInstructionNID);
            _logger.Info("==========================");
            try
            {
                using (var connectionH2H = _context.CreateConnectionH2H())
                {
                    var queryIns = "SELECT SourceSavingsID, TargetSavingsID, TargetBankAccountID, SourceBankAccountID, TargetSavingsName, Amount, BatchID, ClientID, Success, Type FROM [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction] WHERE BankInstructionNID = @BankInstructionNID";

                    var BankInstruction = await connectionH2H.QuerySingleAsync(queryIns, new { BankInstructionNID = BankInstructionNID });


                    if (BankInstruction.BatchID == 0 && BankInstruction.Success)
                    {
                        if (!bypass)
                        {
                            _logger.Info(" BANKINSTRUCTION ALREADY SEND " + BankInstructionNID + "CLIENT ID : " + BankInstruction.ClientID);
                            _logger.Info(" END ONLINE TRANSFER " + BankInstructionNID);
                            _logger.Info("==========================");
                            return result;
                        }
                    }


                    var queryH2H = "SELECT BankInstructionNID, BatchID, StatusCode FROM [dbo].[NIAGA_AccountBankInstruction] WHERE BankInstructionNID = @BankInstructionNID";

                    var H2HInstruction = await connectionH2H.QueryAsync(queryH2H, new { BankInstructionNID = BankInstructionNID });

                    if (H2HInstruction.Any())
                    {
                        dynamic temp = null;
                        temp = H2HInstruction.ToList();

                        foreach (dynamic item in temp)
                        {
                            if (item.BatchID > 0 && item.StatusCode == "00")
                            {
                                _logger.Info(" BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID);
                                _logger.Info(" END ONLINE TRANSFER " + BankInstructionNID);
                                _logger.Info("==========================");
                                result.responseMessage = $"BANKINSTRUCTION ALREADY SEND : " + BankInstructionNID + " CLIENT ID : " + BankInstruction.ClientID;
                                return result;
                            }
                        }
                    }

                    int BatchID = BankInstruction.BatchID;
                    if (BatchID == 0)
                    {
                        BatchID = (await connectionH2H.QueryAsync<int>("SELECT MAX(BatchID)+1 as BatchID From [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction]")).SingleOrDefault();
                    }

                    RestResponse response = new RestResponse();
                    CIMB_OnlineTransfer objbank = new CIMB_OnlineTransfer();


                    if (BankInstruction.TargetBankAccountID == BankInstruction.SourceBankAccountID)
                    {
                        InHouseTransferRequest req = new InHouseTransferRequest();

                        Random rnd = new Random();
                        string randomTransferId = Convert.ToString(rnd.Next(100000, 10000000));
                        string randomRequestId = Convert.ToString(rnd.Next(100000, 10000000));

                        req.transferId = randomTransferId;
                        req.txnDate = DateTime.Now.ToString("yyyyMMdd");
                        req.debitAcctNo = BankInstruction.SourceSavingsID;
                        req.benAcctNo = BankInstruction.TargetSavingsID;
                        req.benName = BankInstruction.TargetSavingsName;
                        req.benBankName = "CIMB NIAGA";
                        req.benBankAddr1 = "Senayan, Jakarta, Kota Jakarta Pusat";
                        req.benBankBranch = "KANTOR PUSAT";
                        req.benBankCode = "022";
                        req.benBankSWIFT = "XXX";
                        req.currCd = "IDR";
                        req.amount = BankInstruction.Amount.ToString("0.00");
                        req.memo = "$S21$-" + BankInstructionNID + "-" + BankInstruction.ClientID;
                        req.requestID = randomRequestId;
                        req.serviceCode = "ACCOUNT_TRANSFER";
                        req.txnRequestDateTime = DateTime.Now.ToString("yyyyMMddHHmmss");

                        string Token = await getCIMBToken(req.txnRequestDateTime, req.requestID, req.serviceCode);

                        req.token = Token;

                        StringBuilder XML = new StringBuilder();
                        string ns1 = "http://schemas.xmlsoap.org/soap/envelope/";
                        string ns2 = "http://10.25.136.152";
                        string ns3 = "java:prismagateway.service.HostCustomer";
                        XML.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><soapenv:Envelope xmlns:soapenv=\"{ns1}\" xmlns:ns=\"{ns2}\" xmlns:java=\"{ns3}\">");
                        XML.Append($@"<soapenv:Header/>
                        <soapenv:Body>
                        <ns:HostCustomer>
                        <ns:input>
                        <java:tokenAuth>{Token}</java:tokenAuth>
                        <java:txnData>
                        <![CDATA[<transferRequest><transfer>
                        <transferId>{req.transferId}</transferId>
                        <txnDate>{req.txnDate}</txnDate>
                        <debitAcctNo>{req.debitAcctNo}</debitAcctNo>
                        <benAcctNo>{req.benAcctNo}</benAcctNo>
                        <benName>{req.benName}</benName>
                        <benBankName>{req.benBankName}</benBankName>
                        <benBankAddr1>{req.benBankAddr1}</benBankAddr1><benBankAddr2>
                        </benBankAddr2><benBankAddr3></benBankAddr3>
                        <benBankBranch>{req.benBankBranch}</benBankBranch>
                        <benBankCode>{req.benBankCode}</benBankCode>
                        <benBankSWIFT>{req.benBankSWIFT}</benBankSWIFT>
                        <currCd>{req.currCd}</currCd>
                        <amount>{req.amount}</amount>
                        <memo>{req.memo}</memo>
                        </transfer></transferRequest>]]>
                        </java:txnData>
                        <java:serviceCode>{req.serviceCode}</java:serviceCode>
                        <java:corpID>ID00009KIMENG</java:corpID>
                        <java:requestID>{req.requestID}</java:requestID>
                        <java:txnRequestDateTime>{req.txnRequestDateTime}</java:txnRequestDateTime>
                        </ns:input></ns:HostCustomer>
                        </soapenv:Body></soapenv:Envelope>");


                        response = await sendToCIMB(XML.ToString());
                        result = response.Content;

                        string statuscode = "";
                        string statusMessage = "";

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var conten = response.Content;
                            XmlDataDocument doc = new XmlDataDocument();
                            doc.LoadXml(conten);

                            XmlElement root = doc.DocumentElement;

                            string text = root.InnerText;
                            string text1 = text.Replace("\n", "");
                            string text2 = text1.Replace(" ", "");
                            statuscode = text2.Substring(0, 2);
                            if (statuscode == "00")
                            {
                                statusMessage = "successfull";
                            }
                            else
                            {
                                statuscode = "01";
                                statusMessage = text2;
                            }


                            objbank.BankInstructionNID = BankInstructionNID;
                            objbank.BatchID = BankInstruction.BatchID;
                            objbank.ClientID = BankInstruction.ClientID;
                            objbank.BatchID = BatchID;
                            objbank.BenAcctNo = req.benAcctNo;
                            objbank.DebitAcctNo = req.debitAcctNo;
                            objbank.BenName = req.benName;
                            objbank.Amount = req.amount;
                            objbank.BankInstructionType = BankInstruction.Type;
                            objbank.EntryTime = DateTime.Now;
                            objbank.StatusCode = "00";
                            objbank.StatusMessage = result;
                            objbank.TransferId = req.transferId;
                            objbank.TxnDate = req.txnDate;
                            objbank.BenBankAddr1 = req.benBankAddr1;
                            objbank.BenBankBranch = req.benBankBranch;
                            objbank.BenBankCode = req.benBankCode;
                            objbank.BenBankSWIFT = req.benBankSWIFT;
                            objbank.BenBankCity = "";
                            objbank.BenBankCountry = "";
                            objbank.CurrCd = "";
                            objbank.Memo = req.memo;
                            objbank.InstructDate = "";
                            objbank.RequestID = req.requestID;
                            objbank.ServiceCode = req.serviceCode;
                            objbank.CorpID = "ID00009KIMENG";
                            objbank.TxnRequestDateTime = req.txnRequestDateTime;
                            objbank.SendTime = DateTime.Now;

                            string query = @"INSERT INTO [dbo].[NIAGA_AccountBankInstruction]
                                        ([BankInstructionNID],[BatchID],[ClientID],[DebitAcctNo],[BenAcctNo],[BenName],[Amount]
                                        ,[BankInstructionType],[EntryTime],[StatusCode],[StatusMessage],[TransferId],[TxnDate],[BenBankName]
                                        ,[BenBankAddr1],[BenBankBranch],[BenBankCode],[BenBankSWIFT]
                                        ,[BenBankCity],[BenBankCountry],[CurrCd],[Memo],[InstructDate]
                                        ,[RequestID],[ServiceCode],[CorpID],[TxnRequestDateTime]
                                        ,[SendTime])
                                                VALUES
                                        (@BankInstructionNID,@BatchID,@ClientID,@DebitAcctNo,@BenAcctNo,@BenName,@Amount
                                        ,@BankInstructionType,@EntryTime,@StatusCode,@StatusMessage,@TransferId,@TxnDate,@BenBankName
                                        ,@BenBankAddr1,@BenBankBranch,@BenBankCode,@BenBankSWIFT
                                        ,@BenBankCity,@BenBankCountry,@CurrCd,@Memo,@InstructDate
                                        ,@RequestID,@ServiceCode,@CorpID,@TxnRequestDateTime                                        
                                        ,@SendTime)  SELECT scope_identity()";

                            objbank.AutoNID = await connectionH2H.ExecuteScalarAsync<long>(query, objbank);
                            _logger.Info("         [NIAGA_AccountBankInstruction] inserted " + objbank.AutoNID);

                            string queryNiaga = "UPDATE [dbo].[NIAGA_AccountBankInstruction] SET BatchID = @BatchID WHERE BankInstructionNID = @BankInstructionNID";
                            var ph2h = new DynamicParameters();
                            ph2h.Add("@BatchID", BatchID);
                            ph2h.Add("@BankInstructionNID", BankInstructionNID);
                            await connectionH2H.ExecuteAsync(queryNiaga, ph2h);

                            _logger.Info("         [NIAGA_AccountBankInstruction] updated " + BankInstructionNID);

                            queryNiaga = "UPDATE [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction] SET BatchID = @BatchID, Success = @Success, LastUpdate = @LastUpdate WHERE BankInstructionNID = @BankInstructionNID";
                            var pbo = new DynamicParameters();
                            pbo.Add("@BatchID", BatchID);
                            pbo.Add("@Success", true);
                            pbo.Add("@LastUpdate", DateTime.Now);
                            pbo.Add("@BankInstructionNID", BankInstructionNID);
                            await connectionH2H.ExecuteAsync(queryNiaga, pbo);

                            _logger.Info("         [BankInstruction] updated " + BankInstructionNID);

                            BankInstructionResult objInstructionResult = new BankInstructionResult();
                            objInstructionResult.Date = DateTime.Now;
                            objInstructionResult.BatchID = BatchID;
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

                            string queryResulut = @"INSERT INTO [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstructionResult]
                                        ([Date],[Bank],[Action],[BatchID],[AutoID],[FileName]
                                        ,[ResultText],[LastUpdate],[UserNID],[SysUserID],[TerminalID])
                                             VALUES
                                        (@Date,@Bank,@Action,@BatchID,@AutoID,@FileName
                                        ,@ResultText,@LastUpdate,@UserNID,@SysUserID,@TerminalID)";

                            await connectionH2H.ExecuteAsync(queryResulut, objInstructionResult);
                            _logger.Info("         [BankInstructionResult] inserted " + objInstructionResult.BatchID);

                        }

                        //if (statuscode == "00")
                        //{

                        //    string queryNiaga = "UPDATE [dbo].[NIAGA_AccountBankInstruction] SET BatchID = @BatchID";
                        //    var ph2h = new DynamicParameters();
                        //    ph2h.Add("@BatchID", BatchID);
                        //    await connectionH2H.ExecuteAsync(queryNiaga, ph2h);

                        //    _logger.Info("         [NIAGA_AccountBankInstruction] updated " + BankInstructionNID);

                        //    queryNiaga = "UPDATE [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction] SET BatchID = @BatchID, Success = @Success, LastUpdate = @LastUpdate WHERE BankInstructionNID = @BankInstructionNID";
                        //    var pbo = new DynamicParameters();
                        //    pbo.Add("@BatchID", BatchID);
                        //    pbo.Add("@Success", true);
                        //    pbo.Add("@LastUpdate", DateTime.Now);
                        //    pbo.Add("@BankInstructionNID", BankInstructionNID);
                        //    await connectionH2H.ExecuteAsync(queryNiaga, pbo);

                        //    _logger.Info("         [BankInstruction] updated " + BankInstructionNID);
                        //}
                        //else
                        //{
                        //    string queryNiaga = "UPDATE [dbo].[NIAGA_AccountBankInstruction] SET BatchID = @BatchID";
                        //    var ph2h = new DynamicParameters();
                        //    ph2h.Add("@BatchID", 0);
                        //    await connectionH2H.ExecuteAsync(queryNiaga, ph2h);

                        //    _logger.Info("         [NIAGA_AccountBankInstruction] updated " + BankInstructionNID);

                        //    queryNiaga = "UPDATE [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction] SET BatchID = @BatchID, Success = @Success, Rejected = @Rejected, LastUpdate = @LastUpdate WHERE BankInstructionNID = @BankInstructionNID";
                        //    var pbo = new DynamicParameters();
                        //    pbo.Add("@BatchID", 0);
                        //    pbo.Add("@Success", false);
                        //    pbo.Add("@Rejected", true);
                        //    pbo.Add("@LastUpdate", DateTime.Now);
                        //    pbo.Add("@BankInstructionNID", BankInstructionNID);
                        //    await connectionH2H.ExecuteAsync(queryNiaga, pbo);

                        //    _logger.Info("         [BankInstruction] updated " + BankInstructionNID);
                        //}

                        //string queryNiaga = "UPDATE [dbo].[NIAGA_AccountBankInstruction] SET BatchID = @BatchID";
                        //var ph2h = new DynamicParameters();
                        //ph2h.Add("@BatchID", BatchID);
                        //await connectionH2H.ExecuteAsync(queryNiaga, ph2h);

                        //_logger.Info("         [NIAGA_AccountBankInstruction] updated " + BankInstructionNID);

                        //queryNiaga = "UPDATE [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstruction] SET BatchID = @BatchID, Success = @Success, LastUpdate = @LastUpdate WHERE BankInstructionNID = @BankInstructionNID";
                        //var pbo = new DynamicParameters();
                        //pbo.Add("@BatchID", BatchID);
                        //pbo.Add("@Success", true);
                        //pbo.Add("@LastUpdate", DateTime.Now);
                        //pbo.Add("@BankInstructionNID", BankInstructionNID);
                        //await connectionH2H.ExecuteAsync(queryNiaga, pbo);

                        //_logger.Info("         [BankInstruction] updated " + BankInstructionNID);

                        //BankInstructionResult objInstructionResult = new BankInstructionResult();
                        //objInstructionResult.Date = DateTime.Now;                        
                        //objInstructionResult.BatchID = BatchID;
                        //objInstructionResult.AutoID = 1;
                        //objInstructionResult.LastUpdate = DateTime.Now;
                        //objInstructionResult.Bank = BankInstruction.SourceBankAccountID;
                        //if (BankInstruction.Type == "CRE")
                        //{
                        //    objInstructionResult.Action = "Credit";
                        //}
                        //else if (BankInstruction.Type == "COL")
                        //{
                        //    objInstructionResult.Action = "Collection";
                        //}
                        //else
                        //{
                        //    objInstructionResult.Action = "Private";
                        //}

                        //objInstructionResult.SysUserID = UtilityClass.SysUserID;
                        //objInstructionResult.TerminalID = UtilityClass.CompH2H;
                        //objInstructionResult.UserNID = UtilityClass.UserMaker;
                        //objInstructionResult.ResultText = response.Content;
                        //objInstructionResult.FileName = "";

                        //string queryResulut = @"INSERT INTO [10.236.10.25].[S21Plus_ZP].[dbo].[BankInstructionResult]
                        //            ([Date],[Bank],[Action],[BatchID],[AutoID],[FileName]
                        //            ,[ResultText],[LastUpdate],[UserNID],[SysUserID],[TerminalID])
                        //                 VALUES
                        //            (@Date,@Bank,@Action,@BatchID,@AutoID,@FileName
                        //            ,@ResultText,@LastUpdate,@UserNID,@SysUserID,@TerminalID)";

                        //await connectionH2H.ExecuteAsync(queryResulut, objInstructionResult);
                        //_logger.Info("         [BankInstructionResult] inserted " + objInstructionResult.BatchID);

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(" **** EXC INHOUSE ONLINE TRANSFER " + ex.Message);
            }
            _logger.Info(" END INHOUSE ONLINE TRANSFER " + BankInstructionNID);
            _logger.Info("==========================");
            return result;
        }

        [Obsolete]
        public async Task<RestResponse> sendToCIMB(string body)
        {
            RestResponse result = new RestResponse();

            _logger.Info("sendToCIMB");
            try
            {

                Settings settings = CIMBUtility.cimbSettings;
                var client = new RestClient(settings.URL);
                client.AddDefaultHeader("Content-Type", "text/xml");
                client.AddDefaultHeader("SOAPAction", settings.URL);

                var Request = new RestRequest();
                Request.Method = Method.Post;
                Request.AddParameter("text/xml", body, ParameterType.RequestBody);

                RestResponse response = client.Execute(Request);

                return response;

            }
            catch (Exception ex)
            {
                _logger.Error(" **** EXC sendToBCA : " + ex.Message);
            }

            return result;
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void tes()
        {
            _logger.Info("getCIMBToken");
        }
    }

    public class CIMBDepositService : ICIMBDepositService
    {
        private readonly DapperContext _context;
        private readonly Logger _logger;

        public CIMBDepositService(DapperContext context)
        {
            _logger = LogManager.GetLogger("CIMBDeposit");
            _context = context;
        }

        public InvestorAcctStatementClass DeserializeInvestorAcct(byte[] content)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(System.Text.Encoding.UTF8.GetString(content).Replace("\0", ""));
            XmlNodeReader xmlNode = new XmlNodeReader(xmlDoc.DocumentElement);
            var serializer = new XmlSerializer(typeof(InvestorAcctStatementClass));
            var instance = (InvestorAcctStatementClass)serializer.Deserialize(xmlNode);
            return instance;
        }

        public async Task<Output> doReceivePushData(Input input)
        {
            _logger.Info($"");
            string PROC_NAME = "doReceivePushData";
            _logger.Info($"Receive {PROC_NAME}");
            string skipReason = "";
            string delimiter = "||";
            decimal Amount = 0;
            string Desc = "";
            string Deskrispi = "";
            bool skipped = false;
            string SavingsID = "";
            try
            {
                byte[] data = Convert.FromBase64String(input.fileContents);

                InvestorAcctStatementClass obj = DeserializeInvestorAcct(data);

                string Extref = obj.fields.Where(x => x.name == "ExtRef").SingleOrDefault().Text;
                string SeqNum = obj.fields.Where(x => x.name == "SeqNum").SingleOrDefault().Text;
                string AC = obj.fields.Where(x => x.name == "AC").SingleOrDefault().Text;
                string CurCod = obj.fields.Where(x => x.name == "CurCod").SingleOrDefault().Text;
                string ValDate = obj.fields.Where(x => x.name == "ValDate").SingleOrDefault().Text;
                string OpenBal = obj.fields.Where(x => x.name == "OpenBal").SingleOrDefault().Text;
                string CloseBal = obj.fields.Where(x => x.name == "CloseBal").SingleOrDefault().Text;
                string Notes = obj.fields.Where(x => x.name == "Notes").SingleOrDefault().Text;
                string ExtRef2 = obj.lists.records.fields.Where(x => x.name == "ExtRef").SingleOrDefault().Text;
                string TrxType = obj.lists.records.fields.Where(x => x.name == "TrxType").SingleOrDefault().Text;
                string DC = obj.lists.records.fields.Where(x => x.name == "DC").SingleOrDefault().Text;
                string CashVal = obj.lists.records.fields.Where(x => x.name == "CashVal").SingleOrDefault().Text;
                string Desc1 = obj.lists.records.fields.Where(x => x.name == "Description1").SingleOrDefault().Text;
                string Desc2 = obj.lists.records.fields.Where(x => x.name == "Description2").SingleOrDefault().Text;
                string Desc3 = obj.lists.records.fields.Where(x => x.name == "Description3").SingleOrDefault().Text;

                StringBuilder wl = new StringBuilder();
                wl.Append("          Extref " + Extref);
                wl.Append("\r\n          SeqNum " + SeqNum);
                wl.Append("\r\n          AC " + AC);
                wl.Append("\r\n          CurCod " + CurCod);
                wl.Append("\r\n          ValDate " + ValDate);
                wl.Append("\r\n          OpenBal " + OpenBal);
                wl.Append("\r\n          CloseBal " + CloseBal);
                wl.Append("\r\n          Notes " + Notes);
                wl.Append("\r\n          ExtRef2 " + ExtRef2);
                wl.Append("\r\n          TrxType " + TrxType);
                wl.Append("\r\n          DC " + DC);
                wl.Append("\r\n          CashVal " + CashVal);
                wl.Append("\r\n          Desc1 " + Desc1);
                wl.Append("\r\n          Desc2 " + Desc2);
                wl.Append("\r\n          Desc3 " + Desc3);

                _logger.Info($"{wl.ToString()}");

                SavingsID = await getNoRDN(AC);
                Amount = await getAmount(CashVal);

                if (string.IsNullOrEmpty(Desc3) || string.IsNullOrEmpty(Desc2) || string.IsNullOrEmpty(Desc1))
                {
                    Desc = "";
                }

                Deskrispi = Desc1 + delimiter + Desc2 + delimiter + Desc3;

                // FORMAT LINK SERVER
                if (DC == "C" || TrxType == "198")
                {
                    string queryNA = @"SELECT count (Extref) As Extref FROM [NIAGA_AccountStatement] WHERE Extref = @Extref";
                    using (var rdnConn = _context.CreateConnectionH2H())
                    {
                        int xExtref = await rdnConn.ExecuteScalarAsync<int>(queryNA, new { Extref = Extref });

                        if (xExtref <= 0)
                        {
                            #region INSERT NIAGA ACCOUNTSTATEMENT
                            string query = @"INSERT INTO [NIAGA_AccountStatement]
                                        ([ExtRef],[SeqNum],[AC],[CurCod],[ValDate],[OpenBal],[CloseBal],[Notes],[ExtRef2],[TrxType],[DC],[CashVal],[Description]
                                        ,[ReceiveTime],[Status],[Fundnid],[FundInOutNID])
                                 VALUES
                                        (@ExtRef,@SeqNum,@AC,@CurCod,@ValDate,@OpenBal,@CloseBal,@Notes,@ExtRef2,@TrxType,@DC,@CashVal,@Description,@ReceiveTime,
                                        @Status,@Fundnid,@FundInOutNID)";

                            var p = new DynamicParameters();
                            p.Add("@ExtRef", Extref);
                            p.Add("@SeqNum", SeqNum);
                            p.Add("@AC", SavingsID);
                            p.Add("@CurCod", CurCod);
                            p.Add("@ValDate", ValDate);
                            p.Add("@OpenBal", OpenBal);
                            p.Add("@CloseBal", CloseBal);
                            p.Add("@Notes", Notes);
                            p.Add("@ExtRef2", ExtRef2);
                            p.Add("@TrxType", TrxType);
                            p.Add("@DC", DC);
                            p.Add("@CashVal", Amount);
                            p.Add("@Description", Deskrispi);
                            p.Add("@ReceiveTime", DateTime.Now);
                            p.Add("@Status", 0);
                            p.Add("@Fundnid", 0);
                            p.Add("@FundInOutNID", 0);

                            await rdnConn.ExecuteAsync(query, p);

                            _logger.Info($" INSERT DATABASE NIAGA ACCOUNTSTATEMENT SUCCESS");

                            #endregion

                            string queryClient = @"SELECT count (SavingsID) As SavingsID FROM [10.236.10.25].[S21Plus_ZP].[dbo].[Client] WHERE SavingsID = @SavingsID";

                            int xSavingsID = await rdnConn.ExecuteScalarAsync<int>(queryClient, new { SavingsID = SavingsID });

                            if (xSavingsID <= 0)
                            {
                                _logger.Info($"ERROR : accountnumber not found");
                                skipped = true;

                            }
                            else if (Deskrispi.Contains("$S21$"))
                            {
                                _logger.Info($"SKIPPED $S21$ DESCRIPTION " + Deskrispi);
                                skipped = true;
                            }
                            else if (Deskrispi.Contains("ZS21Z"))
                            {

                                _logger.Info($"SKIPPED ZS21Z DESCRIPTION " + Deskrispi);
                                skipped = true;
                            }
                            else if (Deskrispi.Contains("ZP150"))
                            {

                                _logger.Info($"SKIPPED ZP150 DESCRIPTION " + Deskrispi);
                                skipped = true;
                            }
                            else
                            {


                                #region INSERT FUNDINOUT

                                string CIMBDate = Convert.ToString(DateTime.Now);
                                string CIMBDateNew = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                                int month = Convert.ToInt32(CIMBDateNew.Substring(3, 2));
                                int day = Convert.ToInt32(CIMBDateNew.Substring(0, 2));
                                int year = Convert.ToInt32(CIMBDateNew.Substring(6, 4));
                                int hour = Convert.ToInt32(CIMBDateNew.Substring(11, 2));
                                int min = Convert.ToInt32(CIMBDateNew.Substring(14, 2));
                                int sec = Convert.ToInt32(CIMBDateNew.Substring(17, 2));

                                int cutoff_hour = 16;
                                int cutoff_min = 30;
                                int cutoff_sec = 0;

                                DateTime bcadate = new DateTime(year, month, day, hour, min, sec);
                                DateTime cutOff = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, cutoff_hour, cutoff_min, cutoff_sec);
                                DateTime trxDate = new DateTime(year, month, day);

                                if (DateTime.Now > cutOff)
                                    trxDate = trxDate.AddDays(1);
                                DateTime valuedate = await getValueDate(trxDate);
                                _logger.Info("valuedate " + valuedate);
                                DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                                _logger.Info("today " + today);

                                dynamic resultClient = await getClient(SavingsID);

                                query = @"INSERT INTO [10.236.10.25].[S21Plus_ZP].[dbo].[FundInOut]
                                        ([ClientNID],[EntryDate],[ValueDate],[SavingsID],[SavingsBankNID],[BankNID],[CurrencyNID],[CurrencyRate],[BankInstruction],[TransferFee]
                                        ,[TransferAmount],[Amount],[CashRefNID],[BankAccountNo],[BankAccountName],[TradingLimit],[InOut],[Description],[DocumentRef],[Status],[Instruction]
                                        ,[BankOffice],[ChangeNID],[OldFundInOutNID],[NewFundInOutNID],[SettledAmount],[FailedAmount],[EntryTime],[EntryUserNID],[EntryComputerName]
                                        ,[EntryIPAddress],[FundNID],[UseCashRef],[Checked],[Approved],[Revised],[Rejected],[Settled],[Failed],[Selected])
                                    VALUES 
                                        (@ClientNID,@EntryDate,@ValueDate,@SavingsID,@SavingsBankNID,@BankNID,@CurrencyNID,@CurrencyRate,@BankInstruction,@TransferFee,@TransferAmount,
                                         @Amount,@CashRefNID,@BankAccountNo,@BankAccountName,@TradingLimit,@InOut,@Description,@DocumentRef,@Status,@Instruction,@BankOffice,@ChangeNID,@OldFundInOutNID,
                                         @NewFundInOutNID,@SettledAmount,@FailedAmount,@EntryTime,@EntryUserNID,@EntryComputerName,@EntryIPAddress,@FundNID,@UseCashRef,@Checked,@Approved,
                                         @Revised,@Rejected,@Settled,@Failed,@Selected)";


                                var pf = new DynamicParameters();
                                pf.Add("@ClientNID", resultClient.ClientNID);
                                pf.Add("@EntryDate", today);
                                pf.Add("@ValueDate", today);
                                pf.Add("@SavingsID", SavingsID);
                                pf.Add("@SavingsBankNID", 9);
                                pf.Add("@BankNID", resultClient.BankNID);
                                pf.Add("@CurrencyNID", 1);
                                pf.Add("@CurrencyRate", 1);
                                pf.Add("@BankInstruction", "TSF");
                                pf.Add("@TransferFee", 0);
                                pf.Add("@TransferAmount", Convert.ToDecimal(Amount));
                                pf.Add("@Amount", Convert.ToDecimal(Amount));
                                pf.Add("@CashRefNID", 0);
                                pf.Add("@BankAccountNo", resultClient.BankAccountNo);
                                pf.Add("@BankAccountName", resultClient.BankAccountName);
                                pf.Add("@TradingLimit", 0);
                                dynamic tempCash = await GetCashBalance(valuedate, resultClient.ClientNID);
                                pf.Add("@CashBalance", tempCash);
                                if (DC == "C")
                                {
                                    pf.Add("@InOut", "I");
                                    pf.Add("@Description", "DEPOSIT DANA - " + resultClient.ClientID + " - " + resultClient.ClientName + " - " + CIMBDateNew);
                                    pf.Add("@Instruction", false);
                                }
                                else if (TrxType == "198")
                                {
                                    pf.Add("@InOut", "O");
                                    pf.Add("@Description", "TAX - " + resultClient.ClientID + " - " + resultClient.ClientName + " - " + CIMBDateNew);
                                    pf.Add("@Instruction", true);
                                }
                                else
                                {
                                    pf.Add("@InOut", "O");
                                    pf.Add("@Description", "PENARIKAN DANA - " + resultClient.ClientID + " - " + resultClient.ClientName + " - " + CIMBDateNew);
                                    pf.Add("@Instruction", false);
                                }
                                pf.Add("@DocumentRef", Extref);
                                pf.Add("@Status", 0);
                                pf.Add("@BankOffice", "");
                                pf.Add("@ChangeNID", 0);
                                pf.Add("@OldFundInOutNID", 0);
                                pf.Add("@NewFundInOutNID", 0);
                                pf.Add("@SettledAmount", 0);
                                pf.Add("@FailedAmount", 0);
                                pf.Add("@EntryTime", DateTime.Now);
                                pf.Add("@EntryUserNID", 1);
                                pf.Add("@EntryComputerName", "S21H2H");
                                pf.Add("@EntryIPAddress", "172.20.4.41");
                                pf.Add("@FundNID", 0);
                                pf.Add("@ChangeNID", 0);
                                pf.Add("@UseCashRef", false);
                                pf.Add("@Checked", false);
                                pf.Add("@Approved", false);
                                pf.Add("@Revised", false);
                                pf.Add("@Rejected", false);
                                pf.Add("@Settled", false);
                                pf.Add("@Failed", false);
                                pf.Add("@Selected", false);

                                await rdnConn.ExecuteAsync(query, pf);

                                dynamic resultfundinout = await getFundIntOutNID(Extref);

                                var tempFundcheck = await FundInOut_Checked(resultfundinout.FundInOutNID);

                                int fundnid = await Fund_CreateFromFundInOut(resultfundinout.FundInOutNID);
                                if (fundnid == null || fundnid <= 0)
                                {
                                    _logger.Info($" --------FUNDLEDGER CREATE ERROR");
                                }
                                else
                                {
                                    _logger.Info($" --------FundLedger Created : " + fundnid);
                                    var tempApproved = await FundInOutApproved(resultfundinout.FundInOutNID, fundnid);
                                    _logger.Info($" --------FundInOut Approved : " + resultfundinout.FundInOutNID);

                                    var tempLog = await Log_Insert();
                                    _logger.Info($" --------Log Insert" + resultfundinout.FundInOutNID);
                                }
                                _logger.Info($" INSERT DATABASE FUNDINOUT SUCCESS");
                                #endregion

                                #region UPDATE NIAGA ACCOUNTSTATEMENT
                                query = @"UPDATE [NIAGA_AccountStatement] SET Status = @Status, Fundnid = @Fundnid, FundInOutNID = @FundInOutNID WHERE ExtRef = @Extref";

                                var up = new DynamicParameters();

                                if (fundnid == null || fundnid <= 0)
                                {
                                    up.Add("@Status", false);
                                }
                                else
                                {
                                    up.Add("@Status", true);
                                }
                                up.Add("@Fundnid", fundnid);
                                up.Add("@FundInOutNID", resultfundinout.FundInOutNID);
                                up.Add("@Extref", Extref);

                                await rdnConn.ExecuteAsync(query, up);

                                _logger.Info($" UPDATE DATABASE NIAGA ACCOUNTSTATEMENT SUCCESS");

                                #endregion
                            }
                        }
                        else
                        {
                            _logger.Info($"ERROR : external ref already exist");
                        }
                    }
                }
                else
                {
                    using (var rdnConn = _context.CreateConnectionH2H())
                    {
                        #region INSERT NIAGA ACCOUNTSTATEMENT
                        string query = @"INSERT INTO [NIAGA_AccountStatement]
                                        ([ExtRef],[SeqNum],[AC],[CurCod],[ValDate],[OpenBal],[CloseBal],[Notes],[ExtRef2],[TrxType],[DC],[CashVal],[Description]
                                        ,[ReceiveTime],[Status],[Fundnid],[FundInOutNID])
                                 VALUES
                                        (@ExtRef,@SeqNum,@AC,@CurCod,@ValDate,@OpenBal,@CloseBal,@Notes,@ExtRef2,@TrxType,@DC,@CashVal,@Description,@ReceiveTime,
                                        @Status,@Fundnid,@FundInOutNID)";

                        var p = new DynamicParameters();
                        p.Add("@ExtRef", Extref);
                        p.Add("@SeqNum", SeqNum);
                        p.Add("@AC", SavingsID);
                        p.Add("@CurCod", CurCod);
                        p.Add("@ValDate", ValDate);
                        p.Add("@OpenBal", OpenBal);
                        p.Add("@CloseBal", CloseBal);
                        p.Add("@Notes", Notes);
                        p.Add("@ExtRef2", ExtRef2);
                        p.Add("@TrxType", TrxType);
                        p.Add("@DC", DC);
                        p.Add("@CashVal", Amount);
                        p.Add("@Description", Deskrispi);
                        p.Add("@ReceiveTime", DateTime.Now);
                        p.Add("@Status", 0);
                        p.Add("@Fundnid", 0);
                        p.Add("@FundInOutNID", 0);

                        await rdnConn.ExecuteAsync(query, p);

                        _logger.Info($"SKIPPED : Notification Debit");
                        _logger.Info($" INSERT DATABASE NIAGA ACCOUNTSTATEMENT SUCCESS");
                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION {ex.Message}");
            }

            Output result = new Output();
            result.@return = "1";
            result.type = "ns1:Output";

            return result;
        }

        public async Task<dynamic> getNoRDN(string AC)
        {
            dynamic result = null;

            string SavingsID = AC.Substring(7, 12);
            result = SavingsID;
            _logger.Info($"      -- No RDN " + SavingsID);

            return result;
        }

        public async Task<dynamic> getAmount(string CashVal)
        {
            dynamic result = null;

            _logger.Info($" EXE getAmount ");

            int count = CashVal.Length - 5;
            string tempamount = CashVal.Substring(count, 5);

            if (tempamount == ".0000")
            {
                result = decimal.Parse(CashVal.Substring(0, count));
            }
            else
            {
                result = decimal.Parse(CashVal.Substring(0, count + 3));

            }

            _logger.Info(" Amount " + result);

            return result;
        }

        public async Task<dynamic> getClient(string SavingsID)
        {
            dynamic result = null;
            _logger.Info($" EXE SP getClient ");

            try
            {
                using (var rdnConn = _context.CreateConnectionH2H())
                {
                    var query = @"SELECT ClientNID,BankAccountNo,BankAccountName,ClientID,ClientName,BankNID FROM [10.236.10.25].[S21Plus_ZP].[dbo].[Client] WHERE SavingsID = @SavingsID AND MainSavings = 1";

                    var tmp = await rdnConn.QueryAsync(query, new { SavingsID = SavingsID });

                    if (tmp.Count() == 1)
                    {
                        if (tmp.Any())
                            result = tmp.Take(1).SingleOrDefault();
                    }
                    else
                    {
                        query = @"SELECT TOP 1 ClientNID,BankAccountNo,BankAccountName,ClientID,ClientName,BankNID FROM [10.236.10.25].[S21Plus_ZP].[dbo].[Client] WHERE SavingsID = @SavingsID ORDER BY ClientNID Desc";

                        tmp = await rdnConn.QueryAsync(query, new { SavingsID = SavingsID });

                        if (tmp.Any())
                            result = tmp.Take(1).SingleOrDefault();
                    }

                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION getClient {ex.Message}");
            }

            return result;
        }


        public async Task<dynamic> CekClientID()
        {
            dynamic result = null;

            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {
                    string ClientID = "000001";

                    var p = new DynamicParameters();

                    p.Add("@ClientID", ClientID);


                    var tmp = await connection.QueryAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[Client_SelectByClientID]", p, commandType: CommandType.StoredProcedure);

                    _logger.Info($" CLientID {tmp}");
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION GetCashBalance {ex.Message}");
            }

            return result;
        }

        public async Task<dynamic> getFundIntOutNID(string Extref)
        {
            dynamic result = 0;
            _logger.Info($" EXE SP getFundIntOutNID ");
            try
            {
                using (var rdnConn = _context.CreateConnectionH2H())
                {
                    var query = "SELECT FundInOutNID FROM [10.236.10.25].[S21Plus_ZP].[dbo].[FundInOut] WHERE DocumentRef = @Extref";
                    var p = new DynamicParameters();
                    p.Add("@Extref", Extref);

                    var tmp = await rdnConn.QueryAsync(query, p);

                    if (tmp.Any())
                        result = tmp.Take(1).SingleOrDefault();

                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION getFundIntOutNID {ex.Message}");
            }

            return result;
        }

        public async Task<dynamic> GetCashBalance(DateTime valueDate, int ClientNID)
        {
            decimal result = 0;
            _logger.Info($" EXE SP GetCashBalance ");
            try
            {
                var p = new DynamicParameters();
                p.Add("@Date", valueDate);
                p.Add("@ClientNID", ClientNID);
                p.Add("@CashBalance",
                   direction: ParameterDirection.Output,
                   value: 0,
                   dbType: DbType.Decimal
               );

                using (var connection = _context.CreateConnectionH2H())
                {
                    await connection.ExecuteAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[Fund_GetCashBalance]", p, commandType: CommandType.StoredProcedure);

                    var temp = p.Get<dynamic>("@CashBalance");

                    if (temp == null)
                    {
                        result = Convert.ToDecimal(0);
                    }
                    else
                    {
                        result = p.Get<decimal>("@CashBalance");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION GetCashBalance {ex.Message}");
            }

            return result;
        }

        public async Task<DateTime> getValueDate(DateTime dt)
        {
            bool mainFlag = true;
            bool flag = true;
            _logger.Info($" EXE SP getValueDate ");
            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {
                    DateTime thisyear = new DateTime(DateTime.Now.Year, 1, 1);

                    var query = "SELECT Date FROM [10.236.10.25].[S21Plus_ZP].[dbo].[MarketHoliday] WHERE Date >= @Date";
                    var p = new DynamicParameters();
                    p.Add("@Date", thisyear);

                    var lstHoliday = await connection.QueryAsync(query, p);

                    while (mainFlag)
                    {
                        flag = true;
                        if (dt.DayOfWeek == DayOfWeek.Saturday)
                        {
                            dt = dt.AddDays(2);
                            flag = false;
                        }
                        if (dt.DayOfWeek == DayOfWeek.Sunday)
                        {
                            dt = dt.AddDays(1);
                            flag = false;
                        }

                        foreach (var row in lstHoliday)
                        {
                            if (row.Date == dt)
                            {
                                flag = false;
                                dt = dt.AddDays(1);
                                break;
                            }
                        }

                        if (flag)
                            mainFlag = !mainFlag;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION getValueDate {ex.Message}");
            }

            return dt;
        }

        public async Task<dynamic> FundInOut_Checked(int FundInOutNID)
        {
            dynamic result = null;
            _logger.Info($" EXE SP FundInOut_Checked ");
            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {


                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@UserNID", 3561);
                    p.Add("@IPAddress", "192.168.15.23");
                    p.Add("@ComputerName", "WEBSVC-PC");

                    await connection.QueryAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[FundInOut_Checked]", p, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION FundInOut_Checked {ex.Message}");
            }

            return result;
        }

        public async Task<int> Fund_CreateFromFundInOut(int FundInOutNID)
        {
            int result = 0;
            _logger.Info($" EXE SP Fund_CreateFromFundInOut ");
            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {
                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@UserNID", 3561);
                    p.Add("@IPAddress", "192.168.15.23");
                    p.Add("@ComputerName", "WEBSVC-PC");
                    p.Add("@FundNID",
                        direction: ParameterDirection.Output,
                        dbType: DbType.Int32,
                        value: 0
                        );

                    await connection.QueryAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[Fund_CreateFromFundInOut]", p, commandType: CommandType.StoredProcedure);

                    result = p.Get<int>("@FundNID");
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION Fund_CreateFromFundInOut {ex.Message}");
            }

            return result;
        }

        public async Task<bool> FundInOutApproved(int FundInOutNID, int FundNID)
        {
            bool result = false;
            _logger.Info($" EXE SP FundInOutApproved ");
            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {
                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@FundNID", FundNID);
                    p.Add("@UserNID", 3561);
                    p.Add("@IPAddress", "192.168.15.23");
                    p.Add("@ComputerName", "WEBSVC-PC");

                    await connection.QueryAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[FundInOut_Approved]", p, commandType: CommandType.StoredProcedure);

                    result = true;
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION FundInOutApproved {ex.Message}");
            }

            return result;
        }

        public async Task<object> Log_Insert()
        {
            bool status = true;
            _logger.Info($" EXE SP Log_Insert ");
            try
            {
                using (var connection = _context.CreateConnectionH2H())
                {
                    var p = new DynamicParameters();
                    p.Add("@PermissionID", "Fund_Approve");
                    p.Add("@Parameters", "webSvc");
                    p.Add("@XML", "");
                    p.Add("@Status", status);
                    p.Add("@UserNID", 3561);
                    p.Add("@IPAddress", "192.168.15.23");
                    p.Add("@ComputerName", "WEBSVC-PC");

                    await connection.ExecuteAsync("[10.236.10.25].[S21Plus_ZP].[dbo].[Log_Insert]", p, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                _logger.Info($" ** EXCEPTION Log_Insert {ex.Message}");
            }

            return true;
        }

    }
}
