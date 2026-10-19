using EMMS.CustomAttributes;
using EMMS.Service;
using EMMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    /// <summary>Cylinder requests (facility + pharmacist) and orders (CMS). Dispatch runs through the Movement Queue.</summary>
    public class CylinderController : BaseController
    {
        private readonly CylinderOrderService _orderService;
        private readonly CylinderReportService _reportService;

        public CylinderController(CylinderOrderService orderService, CylinderReportService reportService)
        {
            _orderService = orderService;
            _reportService = reportService;
        }

        // Stock by facility and size/content, cylinders with patients too long, requested vs dispatched vs received.
        // Admins and CMS officers see every facility; everyone else sees their own facility.
        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to, int overdueDays = 30)
        {
            return View(await _reportService.LoadDashboardAsync(CurrentUser!, from, to, overdueDays));
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> DashboardExport(DateTime? from, DateTime? to, int overdueDays = 30)
        {
            var vm = await _reportService.LoadDashboardAsync(CurrentUser!, from, to, overdueDays);
            return File(CylinderReportService.ToExcel(vm),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"CylinderDashboard_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        [HttpGet]
        [RequireLogin]
        public async Task<IActionResult> Index()
        {
            return View(await _orderService.LoadIndexAsync(CurrentUser!));
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> SubmitRequest(CylinderIndexViewModel model)
        {
            var errors = await _orderService.SubmitRequestAsync(CurrentUser!, model.Form ?? new CylinderRequestForm());
            if (errors.Any())
            {
                TempData["CylinderError"] = string.Join("\n", errors);
                var vm = await _orderService.LoadIndexAsync(CurrentUser!);
                vm.Form = model.Form ?? new CylinderRequestForm();
                return View("Index", vm);
            }

            TempData["Success"] = "Cylinder request submitted for pharmacist approval.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> ApproveRequest(Guid id) => Result(await _orderService.DecideRequestAsync(CurrentUser!, id, approve: true, null), "Request approved and sent to CMS.");

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> RejectRequest(Guid id, string? reason) => Result(await _orderService.DecideRequestAsync(CurrentUser!, id, approve: false, reason), "Request rejected; the requester can revise and resubmit.");

        // Saves the order, then opens the Movement Queue in dispatch mode for it
        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> CreateOrder(Guid requestId, int quantity)
        {
            var (order, error) = await _orderService.CreateOrderAsync(CurrentUser!, requestId, quantity);
            if (order == null) return Result(error, "");

            TempData["QueueInfo"] = $"Order {order.OrderNumber} created. Scan the {order.Quantity} cylinder(s) to dispatch.";
            return RedirectToAction("Queue", "AssetMovement", new { orderId = order.OrderId });
        }

        [HttpGet]
        [RequireLogin]
        public IActionResult Dispatch(Guid id) => RedirectToAction("Queue", "AssetMovement", new { orderId = id });

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> ModifyOrder(Guid id, int quantity) => Result(await _orderService.ModifyOrderAsync(CurrentUser!, id, quantity), "Order modified. Re-submit it to continue dispatching.");

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> ResubmitOrder(Guid id) => Result(await _orderService.ResubmitOrderAsync(CurrentUser!, id), "Order re-submitted.");

        [HttpPost]
        [RequireLogin]
        public async Task<IActionResult> PlantDown(Guid id, string? note) => Result(await _orderService.PlantDownAsync(CurrentUser!, id, note), "Order closed (plant down); the request was returned to the facility.");

        private IActionResult Result(string? error, string success)
        {
            if (error != null) TempData["CylinderError"] = error;
            else TempData["Success"] = success;
            return RedirectToAction(nameof(Index));
        }
    }
}
