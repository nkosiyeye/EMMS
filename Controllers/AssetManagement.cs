using EMMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace EMMS.Controllers
{
    public class AssetManagement : Controller
    {
        public IActionResult Index(int page = 1, int pageSize = 6)
        {
            
            var allAssets = GetAssets(); 

            // Calculate total items and apply pagination
            int totalItems = allAssets.Count();
            var paginatedAssets = allAssets
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Create the pagination model
            var pagination = new Pagination(totalItems, page, pageSize);

            // Pass data to the view
            ViewData["Pagination"] = pagination;
            ViewData["PageSize"] = pageSize;
            return View(paginatedAssets);
        }
        private List<Asset> GetAssets()
        {
            return new List<Asset>
            {
            new Asset { Id = "AS-001", Category = "Medical", SubCategory = "Patient Monitor", ItemName = "Vital Signs Monitor", Department = "Emergency", Manufacturer = "Mindray", SerialNumber = "PM001", Model = "VS900", Placement = false, Vendor = "MOH", ServiceProvider = "MOH", Date = DateTime.Now, Status = "New", Location = "Lobamba Clinic", FunctionalStatus = "Working" },
            new Asset { Id = "AS-002", Category = "Medical", SubCategory = "MRI Machine", ItemName = "MRI Scanner", Department = "Radiology", Manufacturer = "Siemens", SerialNumber = "MRI002", Model = "MAGNETOM Aera", Placement = false, Vendor = "MOH", ServiceProvider = "MOH", Date = DateTime.Now, Status = "Used", Location = "Mbabane Hospital", FunctionalStatus = "Faulty" },
            new Asset { Id = "AS-003", Category = "Medical", SubCategory = "X-Ray Machine", ItemName = "Digital X-Ray", Department = "Radiology", Manufacturer = "GE Healthcare", SerialNumber = "XR003", Model = "Optima XR220amx", Placement = true, Vendor = "GE Healthcare", ServiceProvider = "GE", Date = DateTime.Now.AddDays(-30), Status = "New", Location = "Siteki Clinic", FunctionalStatus = "Working" },
            new Asset { Id = "AS-004", Category = "Non-Medical", SubCategory = "Generator", ItemName = "Standby Generator", Department = "Maintenance", Manufacturer = "Caterpillar", SerialNumber = "GEN004", Model = "C27", Placement = true, Vendor = "Caterpillar", ServiceProvider = "Caterpillar", Date = DateTime.Now.AddDays(-60), Status = "Used", Location = "Manzini Hospital", FunctionalStatus = "Working" },
            new Asset { Id = "AS-005", Category = "Medical", SubCategory = "Ultrasound Scanner", ItemName = "Portable Ultrasound", Department = "Maternity", Manufacturer = "Philips", SerialNumber = "US005", Model = "ClearVue 350", Placement = false, Vendor = "Philips", ServiceProvider = "Philips", Date = DateTime.Now.AddDays(-90), Status = "Refurbished", Location = "Mbabane Hospital", FunctionalStatus = "Working" },
            new Asset { Id = "AS-006", Category = "Medical", SubCategory = "Defibrillator", ItemName = "AED", Department = "Emergency", Manufacturer = "Zoll", SerialNumber = "DEF006", Model = "AED Plus", Placement = true, Vendor = "Zoll", ServiceProvider = "Zoll", Date = DateTime.Now.AddDays(-120), Status = "New", Location = "Lobamba Clinic", FunctionalStatus = "Working" },
            new Asset { Id = "AS-007", Category = "Non-Medical", SubCategory = "Air Conditioner", ItemName = "Wall AC Unit", Department = "IT Office", Manufacturer = "Samsung", SerialNumber = "AC007", Model = "AR9500T", Placement = false, Vendor = "Samsung", ServiceProvider = "Samsung", Date = DateTime.Now.AddDays(-150), Status = "Used", Location = "Manzini Hospital", FunctionalStatus = "Faulty" },
            new Asset { Id = "AS-008", Category = "Medical", SubCategory = "Oxygen Concentrator", ItemName = "O2 Concentrator", Department = "ICU", Manufacturer = "ResMed", SerialNumber = "OC008", Model = "Mobi", Placement = true, Vendor = "ResMed", ServiceProvider = "ResMed", Date = DateTime.Now.AddDays(-180), Status = "New", Location = "Siteki Clinic", FunctionalStatus = "Working" }
        };
        }

        public IActionResult Detail(string id)
        {
            var asset = GetAssets().FirstOrDefault(x => x.Id == id);
            return View(asset);
        }
        public IActionResult registerAsset()
        {
            ViewData["assetTag"] = "AS-" + (GetAssets().Count+1).ToString("D3");
            return View();
        }
    }
}
