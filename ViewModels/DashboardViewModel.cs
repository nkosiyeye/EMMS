using EMMS.Models.Admin;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static EMMS.Models.Enumerators;

namespace EMMS.ViewModels
{
    public class DashboardViewModel : PageModel
    {
        private readonly AssetService _assetService;
        private readonly User currentUser;
        public DashboardViewModel(AssetService assetService, User user)
        {
            _assetService = assetService;
            currentUser = user;
        }

        // In your PageModel (e.g., Index.cshtml.cs)
        public Dictionary<string, int> MovementReasonCounts { get; set; }
        public Dictionary<string, int> FunctionalStatusCounts { get; set; }
        public Dictionary<string, int> ProcurementStatusCounts { get; set; }
        public Dictionary<string, int> DecommissionedByFacilityCounts { get; set; }

        public async Task OnGetAsync()
        {
            var viewModel = await _assetService.GetAssetIndexViewModel(currentUser);

            MovementReasonCounts = viewModel.assetViewModels
                .Where(vm => vm.LastMovement != null)
                .GroupBy(vm => vm.LastMovement.Reason)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Count()
                );

            FunctionalStatusCounts = viewModel.assetViewModels
                .Where(vm => vm.LastMovement != null)
                .GroupBy(vm => vm.LastMovement.FunctionalStatus)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.Count()
                );

            ProcurementStatusCounts = viewModel.assetViewModels
                        .GroupBy(vm => vm.Asset.StatusId)
                        .ToDictionary(
                            g => ((ProcurementStatus)g.Key).ToString(),
                            g => g.Count()
                        );
            DecommissionedByFacilityCounts = viewModel.assetViewModels
                        .Where(vm => vm.LastMovement != null
                                  && (vm.LastMovement.Reason == MovementReason.Decommission
                                      || vm.LastMovement.FunctionalStatus == FunctionalStatus.NonFunctional)
                                  && vm.LastMovement.FacilityId != null)
                        .GroupBy(vm => vm.LastMovement.Facility?.FacilityName ?? "Unknown Facility")
                        .OrderByDescending(g => g.Count())   // sort by highest count
                        .Take(10)                            // pick top 10
                        .ToDictionary(
                            g => g.Key,
                            g => g.Count()
                        );




        }

    }
}
