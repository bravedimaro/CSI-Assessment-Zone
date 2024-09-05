using CSI_Assessment_Zone.Models;
using CSI_Assessment_Zone.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CSI_Assessment_Zone.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JournalController : ControllerBase
    {
        private readonly JournalService _journalService;

        public JournalController(JournalService journalService)
        {
            _journalService = journalService;
        }

        [HttpPost]
        public async Task<IActionResult> PushJournal([FromBody] JournalEntry entry)
        {
            var result = await _journalService.PushJournal(entry);
            if (result)
            {
                return Ok(new { message = "Journal entry pushed successfully" });
            }
            else
            {
                return StatusCode(500, new { message = "Failed to push journal entry" });
            }
        }
    }
}