using Dapper;
using H2HAPICore.Context;
using H2HAPICore.Model.Permata;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Dynamic;
using System.Threading;

namespace H2HAPICore.Services
{
    public interface IGenericService
    {
        public Task<H2HInject.Response> InsertFundInOut(H2HInject.Request data);

        public Task<string> getSavingsID(string ClientID);
    }

    public class GenericService : IGenericService
    {
        private readonly DapperContext _context;
        private readonly ILoggerManager _logger;

        public GenericService(DapperContext context, ILoggerManager logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<H2HInject.Response> InsertFundInOut(H2HInject.Request data)
        {
            H2HInject.Response result = new H2HInject.Response();

            try
            {
                if (DateTime.Now > UtilityClass.CutOffTime)
                    data.transactionDate = data.transactionDate.AddDays(1);

                DateTime ValueDate = getValueDate(data.transactionDate);


                decimal? cashbalance = 0;
                var BOConnection = _context.CreateConnectionBO();

                string query = "SELECT * FROM [Client] WHERE SavingsID = @SavingsID";
                var LstClient = BOConnection.Query(query, new { SavingsID = data.accountNumber }).ToList();
                if(!LstClient.Any())
                {
                    result.result = false;
                    result.message = "Client Not Found";
                    return result;
                }

                if (LstClient.Count > 1)
                    LstClient = LstClient.Where(x => x.MainSavings == true).ToList();

                if (!LstClient.Any())
                {
                    result.result = false;
                    result.message = "Client Mainsavings not set";
                    return result;
                }

                var client = LstClient.FirstOrDefault();
                result.ClientNID = client.ClientNID;
                result.ClientID = client.ClientID;

                cashbalance = await getCashBalance(ValueDate, client.ClientNID);

                query = @"INSERT INTO [FundInOut]
                             (ClientNID,EntryDate,ValueDate
                            ,SavingsID,SavingsBankNID,BankNID
                            ,CurrencyNID,CurrencyRate,BankInstruction
                            ,TransferFee,TransferAmount,Amount
                            ,CashRefNID,BankAccountNo,BankAccountName
                            ,TradingLimit,CashBalance,InOut
                            ,Description,DocumentRef,Status
                            ,BankOffice,OldFundInOutNID,JournalNID
                            ,NewFundInOutNID,SettledAmount,FailedAmount
                            ,EntryTime,EntryUserNID,EntryComputerName
                            ,EntryIPAddress,FundNID,ChangeNID
                            ,UseCashRef,Checked,Approved,Revised
                            ,Rejected,Settled,Failed,Selected,ToRDD,CollateralInOutNID)
                         VALUES (@ClientNID,@EntryDate,@ValueDate
                            ,@SavingsID,@SavingsBankNID,@BankNID
                            ,@CurrencyNID,@CurrencyRate,@BankInstruction
                            ,@TransferFee,@TransferAmount,@Amount
                            ,@CashRefNID,@BankAccountNo,@BankAccountName
                            ,@TradingLimit,@CashBalance,@InOut
                            ,@Description,@DocumentRef,@Status
                            ,@BankOffice,@OldFundInOutNID, @JournalNID
                            ,@NewFundInOutNID,@SettledAmount,@FailedAmount
                            ,@EntryTime,@EntryUserNID,@EntryComputerName
                            ,@EntryIPAddress,@FundNID,@ChangeNID
                            ,@UseCashRef,@Checked,@Approved,@Revised
                            ,@Rejected,@Settled,@Failed,@Selected,@ToRDD,@CollateralInOutNID);
                        SELECT CAST(SCOPE_IDENTITY() as int)";



                var insertParams = new
                {
                    ClientNID = client.ClientNID,
                    EntryDate = DateTime.Now.Date,
                    ValueDate = ValueDate,
                    SavingsID = data.accountNumber,
                    SavingsBankNID = data.BankNID,
                    BankNID = client.BankNID == null ? data.BankNID : client.BankNID,
                    CurrencyNID = 1,
                    CurrencyRate = 1,
                    BankInstruction = "TSF",
                    TransferFee = 0,
                    TransferAmount = Convert.ToDecimal(data.amount),
                    Amount = Convert.ToDecimal(data.amount),
                    CashRefNID = 0,
                    BankAccountNo = client.BankAccountNo,
                    BankAccountName = client.BankAccountName,
                    TradingLimit = 0,
                    CashBalance = cashbalance == null ? 0 : cashbalance.Value,
                    InOut = data.DC == "C" ? 'I' : 'O',
                    Description = data.DC == "C" ? $"FUND NOTIFICATION {data.BankName}" : $"{data.TrxType} PENARIKAN DANA {data.BankName} {client.ClientID}-{client.ClientName} {data.transactionDate.ToString("yyyy-MM-dd")}",
                    DocumentRef = data.ExtRef,
                    Status = 0,
                    RTOrderID = 0,
                    BankOffice = "",
                    OldFundInOutNID = 0,
                    JournalNID = 0,
                    NewFundInOutNID = 0,
                    SettledAmount = 0,
                    FailedAmount = 0,
                    EntryTime = DateTime.Now,
                    EntryUserNID = UtilityClass.UserMaker,
                    EntryComputerName = UtilityClass.CompH2H,
                    EntryIPAddress = UtilityClass.IPAddH2H,
                    FundNID = 0,
                    ChangeNID = 0,
                    IsSystem = true,
                    UseCashRef = false,
                    Checked = false,
                    Approved = false,
                    Revised = false,
                    Rejected = false,
                    Settled = false,
                    Failed = false,
                    Selected = false,
                    ToRDD = false,
                    CollateralInOutNID = 0
                };


                result.FundInOutNID = await BOConnection.ExecuteScalarAsync<int>(query, insertParams);
                if (!result.FundInOutNID.HasValue)
                {
                    _logger.LogWarn($"  FundInOutNID Created Problem : {data.ExtRef}");
                    return result;
                }
                _logger.LogInfo($"  FundInOut Created : {result.FundInOutNID.Value}");

                FundInOutCheck(result.FundInOutNID.Value);
                _logger.LogInfo($"  FundInOut Checked : {result.FundInOutNID.Value}");

                result.FundNID = (await Fund_CreateFromFundInOut(result.FundInOutNID.Value));
                if(result.FundNID.HasValue)
                {
                    _logger.LogWarn($"  FundLedger Created : {result.FundNID.Value}");
                    FundInOutApprove(result.FundInOutNID.Value, result.FundNID.Value);
                    _logger.LogInfo($"  FundInOut Approved : {result.FundInOutNID.Value}");

                    System.Threading.Thread.Sleep(500);
                    LogInsert();
                    _logger.LogInfo("      DONE " + data.ExtRef);

                    result.result = true;
                    result.message = "Success";
                }
                else
                {
                    _logger.LogWarn($"  Ledger Create Problem : {result.FundInOutNID.Value}");
                }
            }
            catch(Exception ex)
            {
                result.result = false;
                result.message = ex.Message;
            }

            return result;
        }

        public DateTime getValueDate(DateTime dt)
        {
            bool mainFlag = true;
            bool flag = true;

            try
            {
                List<MarketHoliday> lstHoliday = new List<MarketHoliday>();
                DateTime thisyear = new DateTime(DateTime.Now.Year, 1, 1);
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    string query = $@"SELECT MarketHolidayNID,MarketNID,Date,Description 
                                    FROM [MarketHoliday] 
                                    WHERE MarketNID = 1 AND Date >= '{thisyear.ToString("yyyy-MM-dd")}'";
                    lstHoliday = BOConnection.Query<MarketHoliday>(query).ToList();
                }
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

                    if(lstHoliday.Exists(x=>x.Date == dt))
                    {
                        flag = false;
                        dt = dt.AddDays(1);
                        break;
                    }

                    if (flag)
                        mainFlag = !mainFlag;
                }
            }
            catch { }

            return dt;
        }

        public async Task<decimal> getCashBalance(DateTime valueDate, int ClientNID)
        {
            decimal result = 0;
            try
            {
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    var p = new DynamicParameters();
                    p.Add("@Date", valueDate);
                    p.Add("@ClientNID", ClientNID);
                    p.Add("@CashBalance",
                          dbType: DbType.Decimal,
                          direction: ParameterDirection.Output);

                    await BOConnection.ExecuteAsync("Fund_GetCashBalance", p, commandType: CommandType.StoredProcedure);

                    result = p.Get<decimal>("@CashBalance");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("getCashBalance " + ex.Message);
            }
            return result;
        }

        public async void FundInOutCheck(int FundInOutNID)
        {
            try
            {
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@UserNID", UtilityClass.UserMaker);
                    p.Add("@IPAddress", UtilityClass.IPAddH2H);
                    p.Add("@ComputerName", UtilityClass.CompH2H);

                    await BOConnection.ExecuteAsync("FundInOut_Checked", p, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("FundInOutCheck " + ex.Message);
            }
        }

        public async void FundInOutApprove(int FundInOutNID, int FundNID)
        {
            try
            {
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@FundNID", FundNID);
                    p.Add("@UserNID", UtilityClass.UserMaker);
                    p.Add("@IPAddress", UtilityClass.IPAddH2H);
                    p.Add("@ComputerName", UtilityClass.CompH2H);

                    await BOConnection.ExecuteAsync("FundInOut_Approved", p, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("FundInOutApprove " + ex.Message);
            }
        }

        public async void LogInsert()
        {
            try
            {
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    var p = new DynamicParameters();
                    p.Add("@PermissionID", "Fund_Approve");
                    p.Add("@Parameters", "webSvc");
                    p.Add("@XML", "");
                    p.Add("@Status", true);
                    p.Add("@UserNID", UtilityClass.UserMaker);
                    p.Add("@IPAddress", UtilityClass.IPAddH2H);
                    p.Add("@ComputerName", UtilityClass.CompH2H);

                    await BOConnection.ExecuteAsync("Log_Insert", p, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("LogInsert " + ex.Message);
            }
        }

        public async Task<int> Fund_CreateFromFundInOut(int FundInOutNID)
        {
            int result = 0;
            try
            {
                using (var BOConnection = _context.CreateConnectionBO())
                {
                    var p = new DynamicParameters();
                    p.Add("@FundInOutNID", FundInOutNID);
                    p.Add("@UserNID", UtilityClass.UserMaker);
                    p.Add("@IPAddress", UtilityClass.IPAddH2H);
                    p.Add("@ComputerName", UtilityClass.CompH2H);
                    p.Add("@FundNID",
                          dbType: DbType.Int32,
                          direction: ParameterDirection.Output);

                    await BOConnection.ExecuteAsync("Fund_CreateFromFundInOut", p, commandType: CommandType.StoredProcedure);

                    result = p.Get<int>("@FundNID");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Fund_CreateFromFundInOut " + ex.Message);
            }
            return result;
        }

        public async Task<string> getSavingsID(string ClientID)
        {
            string result = "";
            using (var BOConnection = _context.CreateConnectionBO())
            {
                string query = "SELECT SavingsID FROM [Client] WHERE ClientID = @ClientID";
                var clientTemp = await BOConnection.QueryAsync<string>(query, new { ClientID = ClientID });
                if (clientTemp.Count() == 1)
                    return clientTemp.SingleOrDefault();
                else
                    return "XXX";
            }

            return result;
        }
    }

    public static class H2HInject
    {
        public class Request
        {
            public string accountNumber { get; set; }
            public DateTime transactionDate { get; set; }
            public string amount { get; set; }
            public string DC { get; set; }
            public string TrxType { get; set; }
            public string ExtRef { get; set; }
            public int BankNID { get; set; }
            public string BankName { get; set; }
        }

        public class Response
        {
            public bool result { get; set; }
            public string message { get; set; }
            public string ClientID { get; set; }
            public int? ClientNID  { get; set; }
            public int? FundInOutNID { get; set; }
            public int? FundNID { get; set; }
        }
    }

    public class MarketHoliday
    {
        public int MarketHolidayNID { get; set; }
        public int MarketNID { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
    }
}
