using H2HAPICore.Model.Permata;
using H2HAPICore.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace H2HAPICore.Controllers
{
    [Route("permata")]
    [ApiController]
    public class PermataController : ControllerBase
    {
        private readonly IPermataService _h2hService;

        public PermataController(IPermataService h2hService)
        {
            _h2hService = h2hService;
        }

        [HttpPost("investor-account/statement")]
        public async Task<IActionResult> InvestorAccountStatement()
        {
            try
            {
                Settings settings = PermataUtility.permataSettings;
                bool IsAuth = false;

                if (string.IsNullOrEmpty(Request.Headers["Authorization"]))
                    return StatusCode(401, "Authentication Failed");

                string authKey = Request.Headers["Authorization"].ToString().Replace("Basic ", "");
                string decodedStr = UtilityClass.Base64Decode(authKey);

                if (decodedStr.IndexOf(':') > 0)
                {
                    string[] tmpAuth = decodedStr.Split(':');
                    if (tmpAuth.Count() <= 1)
                        return StatusCode(401, "Authentication Failed");

                    string clientid = tmpAuth[0];
                    string clientsec = tmpAuth[1];

                    if (clientid == settings.clientid && clientsec == settings.clientsecret)
                    {
                        IsAuth = true;
                    }                   
                        
                }

                string body = string.Empty;

                using (StreamReader reader = new StreamReader(Request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false))
                {
                    body = await reader.ReadToEndAsync();
                }

                InvestorAccountStatementRequest notifData = JsonConvert.DeserializeObject<InvestorAccountStatementRequest>(body);

                var resultToken = await _h2hService.InvestorAccountStatement(notifData, IsAuth);

                return Ok(resultToken);
                    

            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("instruction/token")]
        public async Task<IActionResult> Token()
        {
            try
            {
                var result = _h2hService.GetToken();
                return Ok(result);
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

                var resultInstruction = await _h2hService.OnlineTransfer(BankInstructionNID, false);
                return Ok(resultInstruction);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }
    }

}
