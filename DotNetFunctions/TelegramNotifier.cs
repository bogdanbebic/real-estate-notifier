using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RealEstateNotifier
{
    public class TelegramNotifier
    {
        private readonly ILogger _logger;

        public TelegramNotifier(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TelegramNotifier>();
        }

        [Function("TelegramNotifier")]
        public void Run([TimerTrigger("* 15 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
