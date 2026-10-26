using EMMS.Constants;
using EMMS.Models;
using EMMS.Models.Admin;
using EMMS.Models.Entities;
using EMMS.Service;
using EMMS.ViewModels;
using static EMMS.Models.Enumerators;

namespace EMMS.Tests
{
    public class WorkRequestRulesTests
    {
        private static User MakeUser(UserType type, int facilityId) => new()
        {
            UserId = Guid.NewGuid(),
            FacilityId = facilityId,
            UserRole = new UserRole { UserType = type },
        };

        [Theory]
        [InlineData(WorkState.Open, false, true)]
        [InlineData(WorkState.Open, true, false)]        // a job already exists
        [InlineData(WorkState.InProgress, false, false)]
        [InlineData(WorkState.Completed, false, false)]
        [InlineData(WorkState.Closed, false, false)]
        [InlineData(WorkState.Cancelled, false, false)]
        public void CanEditRequest_And_CanCreateJob(WorkState status, bool hasJob, bool expected)
        {
            Assert.Equal(expected, WorkRequestService.CanEditRequest(status, hasJob));
            Assert.Equal(expected, WorkRequestService.CanCreateJob(status, hasJob));
        }

        [Theory]
        [InlineData(WorkState.Completed, WorkState.Completed, true)]
        [InlineData(WorkState.Open, null, false)]                       // no job: withdraw instead
        [InlineData(WorkState.InProgress, WorkState.InProgress, false)] // job still running
        [InlineData(WorkState.Closed, WorkState.Closed, false)]         // already closed
        [InlineData(WorkState.Cancelled, WorkState.Cancelled, false)]
        public void CanClose(WorkState request, WorkState? job, bool expected)
        {
            Assert.Equal(expected, WorkRequestService.CanClose(request, job));
        }

        [Theory]
        // admin: anything not final
        [InlineData(true, false, WorkState.Open, false, true)]
        [InlineData(true, false, WorkState.InProgress, true, true)]
        [InlineData(true, false, WorkState.Completed, true, true)]
        [InlineData(true, false, WorkState.Closed, true, false)]
        [InlineData(true, false, WorkState.Cancelled, false, false)]
        // requester: only withdraw before a job exists
        [InlineData(false, true, WorkState.Open, false, true)]
        [InlineData(false, true, WorkState.InProgress, true, false)]
        [InlineData(false, true, WorkState.Open, true, false)]
        // anyone else: never
        [InlineData(false, false, WorkState.Open, false, false)]
        public void CanCancel(bool isAdmin, bool isRequester, WorkState status, bool hasJob, bool expected)
        {
            Assert.Equal(expected, WorkRequestService.CanCancel(isAdmin, isRequester, status, hasJob));
        }

        [Fact]
        public void CanUpdateJob_OnlyWhileInProgressAndNotEnded()
        {
            Assert.True(WorkRequestService.CanUpdateJob(WorkState.InProgress, null));
            Assert.False(WorkRequestService.CanUpdateJob(WorkState.InProgress, DateTime.Today));
            Assert.False(WorkRequestService.CanUpdateJob(WorkState.Completed, null));
            Assert.False(WorkRequestService.CanUpdateJob(WorkState.Closed, DateTime.Today));
        }

        [Theory]
        [InlineData(WorkState.InProgress, true)]
        [InlineData(WorkState.Completed, true)]
        [InlineData(WorkState.Open, false)]
        [InlineData(WorkState.Closed, false)]
        [InlineData(WorkState.Cancelled, false)]
        [InlineData(WorkState.Unknown, false)]
        public void IsAllowedJobStatus(WorkState status, bool expected)
        {
            Assert.Equal(expected, WorkRequestService.IsAllowedJobStatus(status));
        }

        [Fact]
        public void NextServiceDate_OnlyForSuccessfulServiceRequestsOnServiceableAssets()
        {
            var closed = new DateTime(2026, 10, 12, 15, 30, 0);
            Assert.Equal(new DateTime(2027, 4, 12), WorkRequestService.NextServiceDate(true, true, true, 6, closed));
            Assert.Null(WorkRequestService.NextServiceDate(false, true, true, 6, closed)); // repair, not a service
            Assert.Null(WorkRequestService.NextServiceDate(true, false, true, 6, closed)); // unsuccessful
            Assert.Null(WorkRequestService.NextServiceDate(true, true, false, 6, closed)); // not serviceable
            Assert.Null(WorkRequestService.NextServiceDate(true, true, true, null, closed));
            Assert.Null(WorkRequestService.NextServiceDate(true, true, true, 0, closed));
        }

        [Fact]
        public void JobNumber_Format()
        {
            Assert.Equal("S003Jb-031", WorkRequestService.FormatJobNumber("S003", 31));
            Assert.Equal("S003Jb-1200", WorkRequestService.FormatJobNumber("S003", 1200));
        }

        [Fact]
        public void ToState_MapsEveryConfiguredStatusName()
        {
            Assert.Equal(WorkState.Open, WorkRequestService.ToState(WorkStatusNames.Open));
            Assert.Equal(WorkState.InProgress, WorkRequestService.ToState(WorkStatusNames.InProgress));
            Assert.Equal(WorkState.Completed, WorkRequestService.ToState(WorkStatusNames.Completed));
            Assert.Equal(WorkState.Cancelled, WorkRequestService.ToState(WorkStatusNames.Cancelled));
            Assert.Equal(WorkState.Closed, WorkRequestService.ToState(WorkStatusNames.Closed));
            Assert.Equal(WorkState.Unknown, WorkRequestService.ToState("Renamed"));
            Assert.Equal(WorkState.Unknown, WorkRequestService.ToState(null));
        }

        [Fact]
        public void StatusMap_RequiresAllFiveStatuses()
        {
            var items = WorkStatusNames.All.Select((n, i) => new LookupItem { Id = 134 + i, Name = n }).ToList();
            var map = new WorkRequestService.StatusMap(items);
            Assert.True(map.IsComplete);
            Assert.Equal(135, map.Id(WorkState.InProgress));
            Assert.Equal(WorkState.Closed, map.State(138));
            Assert.Equal(WorkState.Unknown, map.State(999));

            Assert.False(new WorkRequestService.StatusMap(items.Where(i => i.Name != WorkStatusNames.Cancelled)).IsComplete);
        }

        [Fact]
        public void Access_FacilityAndAssignee()
        {
            var admin = MakeUser(UserType.Administrator, 1);
            var biomed = MakeUser(UserType.Biomed, 2);
            var job = new Job { FacilityId = 3, AssignedTo = Guid.NewGuid() };

            Assert.True(WorkRequestService.CanAccessFacility(admin, 3));
            Assert.True(WorkRequestService.CanAccessFacility(biomed, 2));
            Assert.False(WorkRequestService.CanAccessFacility(biomed, 3));

            Assert.True(WorkRequestService.CanViewJob(admin, job));
            Assert.False(WorkRequestService.CanViewJob(biomed, job));
            job.AssignedTo = biomed.UserId;                 // assigned across facilities
            Assert.True(WorkRequestService.CanViewJob(biomed, job));
        }

        [Theory]
        [InlineData("Fuse", 1, 0, null)]
        [InlineData("Fuse", 2.5, 10, null)]
        [InlineData("", 1, 10, "Select or enter a part.")]
        [InlineData("  ", 1, 10, "Select or enter a part.")]
        [InlineData(null, 1, 10, "Select or enter a part.")]
        [InlineData("Fuse", 0, 10, "Quantity must be greater than zero.")]
        [InlineData("Fuse", -1, 10, "Quantity must be greater than zero.")]
        [InlineData("Fuse", 1, -0.01, "Unit cost cannot be negative.")]
        public void ValidateJobPart(string? name, double quantity, double unitCost, string? expected)
        {
            Assert.Equal(expected, WorkRequestService.ValidateJobPart(name, (decimal)quantity, (decimal)unitCost));
        }

        [Fact]
        public void PartsTotal_SumsQuantityTimesUnitCost()
        {
            var parts = new[]
            {
                new JobPart { Quantity = 2, UnitCost = 12.50m },
                new JobPart { Quantity = 1.5m, UnitCost = 100m },
            };
            Assert.Equal(175m, WorkRequestService.PartsTotal(parts));
            Assert.Equal(0m, WorkRequestService.PartsTotal(Array.Empty<JobPart>()));
        }

        [Theory]
        [InlineData(UserType.Administrator, 2, WorkState.InProgress, true)]     // admin, any facility
        [InlineData(UserType.FacilityManager, 1, WorkState.InProgress, true)]   // manager, own facility
        [InlineData(UserType.FacilityManager, 2, WorkState.InProgress, false)]  // manager, other facility
        [InlineData(UserType.Biomed, 1, WorkState.InProgress, false)]
        [InlineData(UserType.GeneralUser, 1, WorkState.InProgress, false)]
        [InlineData(UserType.Administrator, 1, WorkState.Completed, false)]     // awaiting close
        [InlineData(UserType.Administrator, 1, WorkState.Closed, false)]
        [InlineData(UserType.Administrator, 1, WorkState.Cancelled, false)]
        public void CanReassignJob(UserType type, int userFacility, WorkState status, bool expected)
        {
            var job = new Job { FacilityId = 1 };
            Assert.Equal(expected, WorkRequestService.CanReassignJob(MakeUser(type, userFacility), job, status));
        }

        [Fact]
        public void CanReassignJob_FalseOnceEndDateSet()
        {
            var job = new Job { FacilityId = 1, EndDate = DateTime.Today };
            Assert.False(WorkRequestService.CanReassignJob(MakeUser(UserType.Administrator, 1), job, WorkState.InProgress));
        }

        [Fact]
        public void Workload_GroupsByAssigneeAndCountsStatuses()
        {
            var busy = new User { UserId = Guid.NewGuid(), FirstName = "Ann", LastName = "B" };
            var idle = new User { UserId = Guid.NewGuid(), FirstName = "Zed", LastName = "C" };
            var former = new User { UserId = Guid.NewGuid(), FirstName = "Old", LastName = "D" }; // no longer an active Biomed
            var inProgress = new LookupItem { Name = WorkStatusNames.InProgress };
            var completed = new LookupItem { Name = WorkStatusNames.Completed };
            var jobs = new[]
            {
                new Job { AssignedTo = busy.UserId, User = busy, Status = inProgress },
                new Job { AssignedTo = busy.UserId, User = busy, Status = inProgress },
                new Job { AssignedTo = busy.UserId, User = busy, Status = completed },
                new Job { AssignedTo = former.UserId, User = former, Status = inProgress },
            };

            var model = WorkloadViewModel.Build(jobs, new[] { idle, busy });

            Assert.Equal(new[] { WorkStatusNames.InProgress, WorkStatusNames.Completed }, model.StatusColumns);
            Assert.Equal(new[] { "Ann B", "Old D", "Zed C" }, model.Technicians.Select(t => t.Name));
            var ann = model.Technicians[0];
            Assert.Equal(3, ann.Total);
            Assert.Equal(2, ann.CountsByStatus[WorkStatusNames.InProgress]);
            Assert.Equal(1, ann.CountsByStatus[WorkStatusNames.Completed]);
            Assert.Equal(0, model.Technicians[2].Total);
        }

        [Fact]
        public void CanEditJobEntry_OnlyTheBiomedWhoRecordedIt()
        {
            var original = MakeUser(UserType.Biomed, 1);
            var newAssignee = MakeUser(UserType.Biomed, 1);
            Assert.True(WorkRequestService.CanEditJobEntry(original, original.UserId));
            Assert.False(WorkRequestService.CanEditJobEntry(newAssignee, original.UserId)); // job reassigned to them
            Assert.False(WorkRequestService.CanEditJobEntry(original, null));
        }
    }
}
