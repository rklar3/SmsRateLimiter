using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SmsRateLimiter.Services;
using Xunit;

namespace SmsRateLimiter.Tests
{
    public class SmsRateLimiterTests
    {
        // Tests that a single message can be sent when under the phone number limit
        [Fact]
        public void SingleMessageUnderPhoneNumberLimit()
        {
            var configuration = CreateConfiguration(1, 5);
            var loggerMock = new Mock<ILogger<SmsRateLimiterService>>();
            var service = new SmsRateLimiterService(configuration, loggerMock.Object);
            var phoneNumber = "+12345678901";

            var result = service.CanSendMessage(phoneNumber);

            Assert.True(result);
        }

        // Tests that sending two messages in quick succession to the same number exceeds the limit
        [Fact]
        public void MultipleMessagesExceedsPhoneNumberLimit()
        {
            var configuration = CreateConfiguration(1, 5);
            var loggerMock = new Mock<ILogger<SmsRateLimiterService>>();
            var service = new SmsRateLimiterService(configuration, loggerMock.Object);
            var phoneNumber = "+12345678901";

            var result1 = service.CanSendMessage(phoneNumber);

             // Should exceed limit
            var result2 = service.CanSendMessage(phoneNumber);

            Assert.True(result1);
            Assert.False(result2);
        }

        // Tests that sending messages from multiple numbers can exceed the account-wide limit
        [Fact]
        public void MultipleNumbersExceedsAccountLimit()
        {
            var configuration = CreateConfiguration(5, 3);
            var loggerMock = new Mock<ILogger<SmsRateLimiterService>>();
            var service = new SmsRateLimiterService(configuration, loggerMock.Object);

            var result1 = service.CanSendMessage("+12345678901");
            var result2 = service.CanSendMessage("+12345678902");
            var result3 = service.CanSendMessage("+12345678903");

             // Should exceed account limit
            var result4 = service.CanSendMessage("+12345678904");

            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
            Assert.False(result4);
        }

        // Tests that rate limits reset after one second, allowing new messages to be sent
        [Fact]
        public async Task RateLimitsResetAfterOneSecond()
        {
            var configuration = CreateConfiguration(1, 1);
            var loggerMock = new Mock<ILogger<SmsRateLimiterService>>();
            var service = new SmsRateLimiterService(configuration, loggerMock.Object);
            var phoneNumber = "+12345678901";

            // Act - First send should work
            var result1 = service.CanSendMessage(phoneNumber);
            
            // Second send should fail (over limit)
            var result2 = service.CanSendMessage(phoneNumber);
            
            // Wait for a second to reset
            await Task.Delay(1100);
            
            // Third send should work again (limit reset)
            var result3 = service.CanSendMessage(phoneNumber);

            Assert.True(result1);
            Assert.False(result2);
            Assert.True(result3);
        }

        // Setup Rate Limiter Rules 
        private IConfiguration CreateConfiguration(int phoneLimit, int accountLimit)
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"RateLimits:MaxMessagesPerPhoneNumberPerSecond", phoneLimit.ToString()},
                {"RateLimits:MaxMessagesPerAccountPerSecond", accountLimit.ToString()},
                {"RateLimits:CleanupIntervalMinutes", "60"},
                {"RateLimits:InactivityThresholdHours", "24"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }
    }
} 