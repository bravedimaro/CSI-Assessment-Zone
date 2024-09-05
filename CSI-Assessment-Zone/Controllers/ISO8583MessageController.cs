using CSI_Assessment_Zone.Models;
using CSI_Assessment_Zone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Trx.Communication.Channels.Tcp;

namespace CSI_Assessment_Zone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ISO8583MessageController : ControllerBase
    {
        private readonly ISO8583Service _iSO8583Service;
        private readonly TrxService _trxService;
        private readonly IConfiguration _config;
        public ISO8583MessageController(ISO8583Service iSO8583Service, TrxService trxService, IConfiguration configuration )
        {
            _iSO8583Service=iSO8583Service;
            _trxService=trxService;
            _config = configuration;
        }
        [HttpPost("SendEchoMessage")]
        public async Task<IActionResult> SendEchoMessage()
        {
            
            var result = await _trxService.SendEchoMessage();
           
            return Ok(result);
        }
        [HttpPost("key-exchange")]
        public async Task<IActionResult> SendKeyExchangeMessage()
        {
            var result = await _trxService.SendKeyExchangeMessage();

            return Ok(result);
        }
    }
}
