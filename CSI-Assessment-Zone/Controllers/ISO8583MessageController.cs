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
        private readonly TcpClientChannel _client;
        public ISO8583MessageController(ISO8583Service iSO8583Service, TcpClientChannel tcpClientChannel )
        {
            _iSO8583Service=iSO8583Service;
            _client=tcpClientChannel;
        }
        [HttpGet("SendEchoMessage")]
        public async Task<IActionResult> SendEchoMessage()
        {
            var result = await _iSO8583Service.SendEchoMessage();
           
            return Ok(result);
        }
    }
}
