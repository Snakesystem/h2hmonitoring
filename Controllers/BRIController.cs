using H2HAPICore.Model.BRI;
using H2HAPICore.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace H2HAPICore.Controllers
{
    [Route("bri")]
    [ApiController]
    public class BRIController : ControllerBase
    {
        private readonly IBRIService _h2hService;
        private readonly IGenericService _genericService;

        public BRIController(IBRIService h2hService)
        {
            _h2hService = h2hService;
        }

        [HttpPost("investor-account/statement")]
        public async Task<IActionResult> InvestorAccountStatement()
        {
            try
            {
                string body = string.Empty;

                using (StreamReader reader = new StreamReader(Request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false))
                {
                    body = await reader.ReadToEndAsync();
                }

                InvestorAccountStatementRequest notifData = JsonConvert.DeserializeObject<InvestorAccountStatementRequest>(body);

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

        [HttpGet("instruction/token")]
        public async Task<IActionResult> GetToken()
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
