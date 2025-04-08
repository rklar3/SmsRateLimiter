using Microsoft.AspNetCore.Mvc;
using SmsRateLimiter.Models;
using SmsRateLimiter.Services;

namespace SmsRateLimiter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SmsController : ControllerBase
    {
        private readonly ILogger<SmsController> _logger;
        private readonly ISmsRateLimiterService _rateLimiterService;

        public SmsController(ILogger<SmsController> logger, ISmsRateLimiterService rateLimiterService)
        {
            _logger = logger;
            _rateLimiterService = rateLimiterService;
        }

        [HttpPost("check")]
        public ActionResult<SmsResponse> CheckSmsLimit([FromBody] SmsRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new SmsResponse
                {
                    CanSend = false,
                    Message = "Phone number is required"
                });
            }

            bool canSend = _rateLimiterService.CanSendMessage(request.PhoneNumber);

            var response = new SmsResponse
            {
                CanSend = canSend,
                Message = canSend 
                    ? "Message can be sent" 
                    : "Rate limit exceeded. Please try again later."
            };

            return Ok(response);
        }

        [HttpGet("status")]
        public ActionResult<string> GetStatus()
        {
            return Ok("SMS Rate Limiter is running");
        }
    }
} 