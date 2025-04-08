using System;

namespace SmsRateLimiter.Models
{
    public class SmsRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RecipientNumber { get; set; } = string.Empty;
    }

    public class SmsResponse
    {
        public bool CanSend { get; set; }
        public string? Message { get; set; }
    }
} 