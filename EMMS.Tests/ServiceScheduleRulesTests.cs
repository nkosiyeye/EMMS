using EMMS.Service;

namespace EMMS.Tests
{
    public class ServiceScheduleRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 22);

        [Theory]
        [InlineData(-30, ServiceWindow.Overdue)]
        [InlineData(-1, ServiceWindow.Overdue)]
        [InlineData(0, ServiceWindow.ThisWeek)]          // due today is not overdue yet
        [InlineData(7, ServiceWindow.ThisWeek)]
        [InlineData(8, ServiceWindow.ThisMonth)]
        [InlineData(30, ServiceWindow.ThisMonth)]
        [InlineData(31, ServiceWindow.Later)]
        public void WindowFor_BucketsByDaysUntilDue(int days, ServiceWindow expected)
        {
            Assert.Equal(expected, ServiceScheduleService.WindowFor(Today.AddDays(days), Today));
        }

        [Fact]
        public void WindowFor_IgnoresTimeOfDay_AndNoDateIsNotScheduled()
        {
            Assert.Equal(ServiceWindow.ThisWeek, ServiceScheduleService.WindowFor(Today.AddHours(1), Today.AddHours(23)));
            Assert.Equal(ServiceWindow.NotScheduled, ServiceScheduleService.WindowFor(null, Today));
        }

        [Theory]
        [InlineData(ServiceScheduleService.ShowDue, ServiceWindow.Overdue, true)]
        [InlineData(ServiceScheduleService.ShowDue, ServiceWindow.ThisWeek, true)]
        [InlineData(ServiceScheduleService.ShowDue, ServiceWindow.ThisMonth, true)]
        [InlineData(ServiceScheduleService.ShowDue, ServiceWindow.Later, false)]
        [InlineData(ServiceScheduleService.ShowDue, ServiceWindow.NotScheduled, false)]
        [InlineData(ServiceScheduleService.ShowOverdue, ServiceWindow.ThisWeek, false)]
        [InlineData(ServiceScheduleService.ShowWeek, ServiceWindow.ThisWeek, true)]
        [InlineData(ServiceScheduleService.ShowMonth, ServiceWindow.ThisWeek, false)]
        [InlineData(ServiceScheduleService.ShowUnscheduled, ServiceWindow.NotScheduled, true)]
        [InlineData(ServiceScheduleService.ShowAll, ServiceWindow.Later, true)]
        [InlineData(ServiceScheduleService.ShowAll, ServiceWindow.NotScheduled, true)]
        public void Matches_Filter(string show, ServiceWindow window, bool expected)
        {
            Assert.Equal(expected, ServiceScheduleService.Matches(window, show));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("bogus")]
        public void NormalizeShow_FallsBackToDue(string? show)
        {
            Assert.Equal(ServiceScheduleService.ShowDue, ServiceScheduleService.NormalizeShow(show));
            Assert.Equal(ServiceScheduleService.ShowOverdue, ServiceScheduleService.NormalizeShow(ServiceScheduleService.ShowOverdue));
        }

        private static List<(DateTime, bool)> Occ(DateTime? due, int? interval, DateTime from, DateTime to) =>
            ServiceScheduleService.Occurrences(due, interval, Today, from, to).ToList();

        [Fact]
        public void Occurrences_DueDateThenProjectedVisits()
        {
            var due = new DateTime(2026, 11, 5);
            Assert.Equal(
                [(due, false), (new DateTime(2027, 2, 5), true), (new DateTime(2027, 5, 5), true)],
                Occ(due, 3, Today, new DateTime(2027, 6, 1)));

            // A later window sees only projections
            Assert.Equal([(new DateTime(2027, 5, 5), true)], Occ(due, 3, new DateTime(2027, 5, 1), new DateTime(2027, 6, 1)));
        }

        [Fact]
        public void Occurrences_NoneWhenOverdueOrUnscheduled_AndNoProjectionWithoutInterval()
        {
            var window = (from: new DateTime(2026, 10, 1), to: new DateTime(2027, 12, 1));
            Assert.Empty(Occ(Today.AddDays(-1), 6, window.from, window.to));
            Assert.Empty(Occ(null, 6, window.from, window.to));
            Assert.Equal([(Today, false)], Occ(Today, null, window.from, window.to));   // due today still shows
            Assert.Equal([(Today, false)], Occ(Today, 0, window.from, window.to));
        }

        [Fact]
        public void Occurrences_MonthEndDoesNotDrift()
        {
            var due = new DateTime(2027, 1, 31);
            Assert.Equal(
                [(due, false), (new DateTime(2027, 2, 28), true), (new DateTime(2027, 3, 31), true)],
                Occ(due, 1, Today, new DateTime(2027, 4, 1)));
        }

        [Theory]
        [InlineData(2026, 10, "2026-09-28", "2026-11-02")]   // Oct 2026 starts Thu, ends Sat
        [InlineData(2026, 6, "2026-06-01", "2026-07-06")]    // starts on a Monday
        [InlineData(2027, 2, "2027-02-01", "2027-03-01")]    // exactly four weeks
        public void MonthGrid_CoversWholeMondayFirstWeeks(int year, int month, string start, string end)
        {
            var (s, e) = ServiceScheduleService.MonthGrid(new DateTime(year, month, 17));
            Assert.Equal(DateTime.Parse(start), s);
            Assert.Equal(DateTime.Parse(end), e);
            Assert.Equal(DayOfWeek.Monday, s.DayOfWeek);
            Assert.Equal(0, (e - s).Days % 7);
        }

        [Theory]
        [InlineData("2027-03", "2027-03-01")]
        [InlineData(null, "2026-10-01")]
        [InlineData("2027-13", "2026-10-01")]
        [InlineData("March", "2026-10-01")]
        public void ParseMonth(string? value, string expected)
        {
            Assert.Equal(DateTime.Parse(expected), ServiceScheduleService.ParseMonth(value, Today));
        }

        [Fact]
        public void ReminderMessage()
        {
            Assert.Null(ServiceScheduleService.ReminderMessage(0, 0));
            Assert.Equal("Service schedule: 1 asset overdue for service, with no service request raised yet.",
                ServiceScheduleService.ReminderMessage(1, 0));
            Assert.Equal("Service schedule: 3 assets overdue for service and 2 assets due within 7 days, with no service request raised yet.",
                ServiceScheduleService.ReminderMessage(3, 2));
            Assert.Equal("Service schedule: 1 asset due within 7 days, with no service request raised yet.",
                ServiceScheduleService.ReminderMessage(0, 1));
        }
    }
}
