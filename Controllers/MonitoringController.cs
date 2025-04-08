using Microsoft.AspNetCore.Mvc;
using SmsRateLimiter.Services;

namespace SmsRateLimiter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController : ControllerBase
    {
        private readonly ISmsRateLimiterService _rateLimiterService;

        public MonitoringController(ISmsRateLimiterService rateLimiterService)
        {
            _rateLimiterService = rateLimiterService;
        }

        [HttpGet("stats")]
        public ActionResult<RateLimiterStats> GetStats()
        {
            return Ok(_rateLimiterService.GetStats());
        }

        [HttpGet("phone/{phoneNumber}")]
        public ActionResult<PhoneNumberStats> GetPhoneStats(string phoneNumber)
        {
            return Ok(_rateLimiterService.GetPhoneNumberStats(phoneNumber));
        }

        [HttpGet("active-numbers")]
        public ActionResult<List<PhoneNumberStats>> GetActiveNumbers()
        {
            return Ok(_rateLimiterService.GetAllActiveNumbers());
        }
    }
} 