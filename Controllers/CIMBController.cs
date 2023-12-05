using H2HAPICore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace H2HAPICore.Controllers
{
    [Route("cimb")]
    [ApiController]
    public class CIMBInstructionController : ControllerBase
    {
        private readonly IGenericService _genericService;
        private readonly ICIMBInstructionService _rdnService;
        private readonly IConfiguration _configuration;

        public CIMBInstructionController(ICIMBInstructionService rdnService, IConfiguration configuration, IGenericService genericService)
        {
            _rdnService = rdnService;
            _configuration = configuration;
            _genericService = genericService;
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

                StringBuilder sb = new StringBuilder(body);



                return Ok("OK");


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

                var resultToken = await _rdnService.OnlineTransfer(BankInstructionNID, false);
                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("manual/instruction")]
        public async Task<IActionResult> ManualInstruction(int BankInstructionNID, string Pass)
        {
            try
            {
                if (Pass != "Diehards21+")
                {
                    return Unauthorized("Enak yeeeee...");
                }

                var resultToken = await _rdnService.OnlineTransfer(BankInstructionNID, true);
                return Ok(resultToken);
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("tes/tis")]
        public async Task<IActionResult> aaa()
        {
            try
            {
                _rdnService.tes();
                return Ok("");
            }
            catch (Exception ex)
            {
                //log error
                return StatusCode(500, ex.Message);
            }
        }

    }
}
