using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace SmsRateLimiter.Services
{
    public interface ISmsRateLimiterService
    {
        bool CanSendMessage(string phoneNumber);
        void CleanupInactiveNumbers();
        RateLimiterStats GetStats();
        PhoneNumberStats GetPhoneNumberStats(string phoneNumber);
        List<PhoneNumberStats> GetAllActiveNumbers();
    }

    public class RateLimiterStats
    {
        public int TotalMessages { get; set; }
        public int AccountLimit { get; set; }
        public DateTime LastReset { get; set; }
        public int ActivePhoneNumbers { get; set; }
        public Dictionary<string, int> MessagesPerSecond { get; set; } = new();
    }

    public class PhoneNumberStats
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public int MessageCount { get; set; }
        public DateTime LastReset { get; set; }
        public DateTime LastUsed { get; set; }
        public int MessagesPerSecond { get; set; }
        public int MessagesLastMinute { get; set; }
        public int MessagesLast5Seconds { get; set; }
        public Queue<DateTime> MessageTimestamps { get; set; } = new();
    }

    /// Service that manages rate limiting for SMS messages
    public class SmsRateLimiterService : ISmsRateLimiterService, IHostedService, IDisposable
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmsRateLimiterService> _log;
        private readonly ConcurrentDictionary<string, PhoneNumberStats> _phoneStats = new();
        private readonly object _accountLock = new();
        
        private int _totalMessages = 0;
        private DateTime _lastAccountReset = DateTime.UtcNow;
        private Timer? _cleanupTimer;
        private readonly TimeSpan _inactiveThreshold = TimeSpan.FromHours(24);
        private readonly Dictionary<string, int> _messagesPerSecond = new();

        private int PhoneNumberLimit => _config.GetValue<int>("RateLimits:MaxMessagesPerPhoneNumberPerSecond", 1);
        private int AccountLimit => _config.GetValue<int>("RateLimits:MaxMessagesPerAccountPerSecond", 5);
        private int CleanupInterval => _config.GetValue<int>("RateLimits:CleanupIntervalMinutes", 60);

        /// Creates a new instance of the SMS rate limiter service
        public SmsRateLimiterService(IConfiguration config, ILogger<SmsRateLimiterService> log)
        {
            _config = config;
            _log = log;
        }

        /// Checks if a message can be sent from the given phone number
        public bool CanSendMessage(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                _log.LogWarning("Empty phone number provided");
                return false;
            }

            // Check if we need to reset the account counter
            ResetAccountCounterIfNeeded();

            // First check the account-wide limit
            lock (_accountLock)
            {
                if (_totalMessages >= AccountLimit)
                {
                    _log.LogInformation("Account limit reached: {current}/{max}", _totalMessages, AccountLimit);
                    return false;
                }
            }

            // Get the stats for this phone number
            var stats = _phoneStats.GetOrAdd(phoneNumber, _ => new PhoneNumberStats { PhoneNumber = phoneNumber });

            // Reset the counter if a second has passed
            if ((DateTime.UtcNow - stats.LastReset).TotalSeconds >= 1)
            {
                stats.MessageCount = 0;
                stats.LastReset = DateTime.UtcNow;
                stats.MessagesPerSecond = 0;
            }

            // Update the last used time for cleanup
            stats.LastUsed = DateTime.UtcNow;

            // Check if this phone number has hit its limit
            if (stats.MessageCount >= PhoneNumberLimit)
            {
                _log.LogInformation("Phone {phone} limit reached: {current}/{max}", 
                    phoneNumber, stats.MessageCount, PhoneNumberLimit);
                return false;
            }

            // All good - increment the counters
            stats.MessageCount++;
            stats.MessagesPerSecond++;
            
            // Add timestamp to message history
            var now = DateTime.UtcNow;
            stats.MessageTimestamps.Enqueue(now);
            
            // Clean up old timestamps and update minute/second counts
            while (stats.MessageTimestamps.Count > 0 && (now - stats.MessageTimestamps.Peek()).TotalMinutes > 1)
            {
                stats.MessageTimestamps.Dequeue();
            }
            
            stats.MessagesLastMinute = stats.MessageTimestamps.Count;
            stats.MessagesLast5Seconds = stats.MessageTimestamps.Count(t => (now - t).TotalSeconds <= 5);

            lock (_accountLock)
            {
                _totalMessages++;
            }

            return true;
        }

        /// Resets the account-wide message counter if a second has passed since the last reset
        private void ResetAccountCounterIfNeeded()
        {
            lock (_accountLock)
            {
                if ((DateTime.UtcNow - _lastAccountReset).TotalSeconds >= 1)
                {
                    _totalMessages = 0;
                    _lastAccountReset = DateTime.UtcNow;
                    foreach (var stats in _phoneStats.Values)
                    {
                        stats.MessagesPerSecond = 0;
                    }
                }
            }
        }

        /// Removes phone numbers that haven't been used for longer than the inactivity threshold
        public void CleanupInactiveNumbers()
        {
            var cutoff = DateTime.UtcNow.Subtract(_inactiveThreshold);
            
            foreach (var phone in _phoneStats.Keys)
            {
                if (_phoneStats.TryGetValue(phone, out var stats) && stats.LastUsed < cutoff)
                {
                    _log.LogInformation("Removing inactive number: {phone}", phone);
                    _phoneStats.TryRemove(phone, out _);
                }
            }
        }

        /// Starts the rate limiter service and initializes the cleanup timer
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation("Starting SMS Rate Limiter");
            
            _cleanupTimer = new Timer(
                _ => CleanupInactiveNumbers(),
                null,
                TimeSpan.FromMinutes(CleanupInterval),
                TimeSpan.FromMinutes(CleanupInterval));

            return Task.CompletedTask;
        }

        /// Stops the rate limiter service and cleans up resources
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation("Stopping SMS Rate Limiter");
            
            _cleanupTimer?.Change(Timeout.Infinite, 0);
            
            return Task.CompletedTask;
        }

        /// Disposes of the cleanup timer
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }

        public RateLimiterStats GetStats()
        {
            lock (_accountLock)
            {
                return new RateLimiterStats
                {
                    TotalMessages = _totalMessages,
                    AccountLimit = AccountLimit,
                    LastReset = _lastAccountReset,
                    ActivePhoneNumbers = _phoneStats.Count,
                    MessagesPerSecond = _phoneStats.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.MessagesPerSecond
                    )
                };
            }
        }

        public PhoneNumberStats GetPhoneNumberStats(string phoneNumber)
        {
            if (_phoneStats.TryGetValue(phoneNumber, out var stats))
            {
                return stats;
            }
            return new PhoneNumberStats { PhoneNumber = phoneNumber };
        }

        public List<PhoneNumberStats> GetAllActiveNumbers()
        {
            return _phoneStats.Values.ToList();
        }
    }
} 