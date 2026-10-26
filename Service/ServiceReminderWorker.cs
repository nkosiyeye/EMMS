namespace EMMS.Service
{
    /// <summary>
    /// Sends the daily service reminders (<see cref="ServiceScheduleService.SendRemindersAsync"/>). Checks every hour
    /// from <see cref="FirstHour"/> onwards; the reminder itself is sent at most once per facility per day, so restarts
    /// and repeated checks do not send it twice.
    /// </summary>
    public class ServiceReminderWorker : BackgroundService
    {
        public const int FirstHour = 7;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ServiceReminderWorker> _logger;

        public ServiceReminderWorker(IServiceScopeFactory scopes, ILogger<ServiceReminderWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                if (DateTime.Now.Hour >= FirstHour)
                    await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var schedule = scope.ServiceProvider.GetRequiredService<ServiceScheduleService>();
                var notified = await schedule.SendRemindersAsync(DateTime.Today);
                if (notified > 0)
                    _logger.LogInformation("Service reminders sent to {Count} facilities.", notified);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Try again on the next tick; a failed reminder must not stop the app.
                _logger.LogError(ex, "Sending service reminders failed.");
            }
        }
    }
}
