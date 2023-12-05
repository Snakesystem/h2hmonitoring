using H2HAPICore.Model.BCA;
using H2HAPICore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace H2HAPICore.Controllers
{
    [Route("bca")]
    [ApiController]
    public class BCAController : ControllerBase
    {
        private readonly IGenericService _genericService;
        private readonly IBCAService _h2hService;
        private readonly IConfiguration _configuration;

        public BCAController(IBCAService h2hService, IConfiguration configuration, IGenericService genericService)
        {
            _h2hService = h2hService;
            _configuration = configuration;
            _genericService = genericService;
        }

        [HttpGet("account/validation")]
        public async Task<IActionResult> ValidationAccount(string accountnumber, string firstname)
        {
            try
            {   

                var resultToken = await _h2hService.ValidationAccount(accountnumber, firstname);

                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("inbound/token")]
        public async Task<IActionResult> CreateToken([FromForm][Required] TokenCreateRequest data)
        {
            try
            {
                if (data == null || string.IsNullOrEmpty(data.grant_type) || data.grant_type != "client_credentials")
                {
                    return StatusCode(401, "Authentication Failed");
                }

                if (!string.IsNullOrEmpty(Request.Headers["Authorization"].ToString()))
                {
                    string authKey = Request.Headers["Authorization"].ToString();

                    var bcaCredentials = BCAUtility.bcaSettings.InBound;
                    string serverAuth = UtilityClass.Base64Encode($"{bcaCredentials.clientid}:{bcaCredentials.clientsecret}");

                    if (authKey != $"Basic {serverAuth}")
                        return StatusCode(401, "Authentication Failed");
                }
                else
                    return StatusCode(401, "Authentication Failed");

                var resultToken = await _h2hService.CreateToken();

                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("investor-account/statement")]
        public async Task<IActionResult> InvestorAccountStatement()
        {
            try
            {
                if (string.IsNullOrEmpty(Request.Headers["Authorization"]) || string.IsNullOrEmpty(Request.Headers["X-BCA-Key"]) || string.IsNullOrEmpty(Request.Headers["X-BCA-Timestamp"]) || string.IsNullOrEmpty(Request.Headers["X-BCA-Signature"]))
                    return StatusCode(401, "Authentication Failed");

                string token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                string x_bca_key = Request.Headers["X-BCA-Key"];
                string x_bca_Timestamp = Request.Headers["X-BCA-Timestamp"];
                string x_bca_Signature = Request.Headers["X-BCA-Signature"];

                var brokerCredential = BCAUtility.bcaSettings.InBound;

                if (x_bca_key != brokerCredential.apikey)
                    return StatusCode(401, "Authentication Failed");

                if (!BCAUtility.validateExpToken(token))
                    return StatusCode(401, "Authentication Failed");

                string body = string.Empty;

                using (StreamReader reader = new StreamReader(Request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false))
                {
                    body = await reader.ReadToEndAsync();
                }


                InvestorAccountStatementRequest notifData = JsonConvert.DeserializeObject<InvestorAccountStatementRequest>(body);
                string signature = await _h2hService.generateSignature(Request.Method, token, "/bca/investor-account/statement", brokerCredential.apikey, brokerCredential.apisecret, body, x_bca_Timestamp);
                if (x_bca_Signature != signature)
                    return StatusCode(401, "Authentication Failed");

                var resultToken = await _h2hService.InvestorAccountStatement(notifData);

                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("inject/statement")]
        public async Task<IActionResult> InjectStatement(string ClientID, string Amount, string Pass)
        {
            try
            {
                if (Pass != "WilliamDima270388")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                string savingsid = await _genericService.getSavingsID(ClientID);
                if (savingsid == "XXX")
                {
                    return Unauthorized("NOT Found");
                }

                InvestorAccountStatementRequest notifData = new InvestorAccountStatementRequest();
                notifData.AccountCredit = "0";
                notifData.AccountDebit = "0";
                notifData.AccountNumber = savingsid;
                notifData.CloseBalance = "0";
                notifData.Currency = "IDR";
                notifData.ExternalReference = UtilityClass.randomString(16, false);
                notifData.OpenBalance = "0";
                notifData.SeqNumber = "0";
                notifData.TxnAmount = Amount;
                notifData.TxnCode = "C";
                notifData.TxnDesc = "INJECT FROM SERVICE -TESTONLY-";
                notifData.TxnType = "NTRF";
                notifData.TxnDate = DateTime.Now.ToString("MMddyyyy HHmmss");

                var resultToken = await _h2hService.InvestorAccountStatement(notifData);

                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("manual/statement")]
        public async Task<IActionResult> ManualStatement(int StatementID, string Pass)
        {
            try
            {
                if (Pass != "WilliamDima270388")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                var resultToken = await _h2hService.ManualPosting(StatementID);
                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("auto/instruction")]
        public async Task<IActionResult> AutomaticInstruction(int BankInstructionNID, string Pass)
        {
            try
            {
                if (Pass != "WilliamDima270388")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                var resultToken = await _h2hService.OnlineTransfer(BankInstructionNID, false);
                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("outbound/token")]
        public async Task<IActionResult> GetBCAToken(string Pass)
        {
            try
            {
                if (Pass != "WilliamDima270388")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                var resultToken = await _h2hService.GetToken();
                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("getdata/account-statement")]
        public async Task<IActionResult> GetAccountStatement(string Pass)
        {
            try
            {
                if (Pass != "WilliamDima270388")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                var resultgetdata = await _h2hService.GetDataAccountStatement();
                return Ok(resultgetdata);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }
    }
}
