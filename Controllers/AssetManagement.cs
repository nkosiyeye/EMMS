using System.Diagnostics;
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
            new Asset
        {
            AssetId = "AS-001",
            Category = "Medical",
            SubCategory = "Patient Monitor",
            ItemName = "Vital Signs Monitor",
            Department = "Emergency",
            Manufacturer = "Mindray",
            SerialNumber = "PM001",
            Model = "VS900",
            IsPlacement = false,
            Vendor = "MOH",
            ServiceProvider = "MOH",
            ProcurementDate = DateTime.Now,
            Status = "New",
            //Location = "Lobamba Clinic", taken from movement history
            //FunctionalStatus = "Working",
            Cost = 5000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 5
        },
        new Asset
        {
            AssetId = "AS-002",
            Category = "Medical",
            SubCategory = "MRI Machine",
            ItemName = "MRI Scanner",
            Department = "Radiology",
            Manufacturer = "Siemens",
            SerialNumber = "MRI002",
            Model = "MAGNETOM Aera",
            IsPlacement = false,
            Vendor = "MOH",
            ServiceProvider = "MOH",
            ProcurementDate = DateTime.Now,
            Status = "Used",
            //Location = "Mbabane Hospital", 
            //FunctionalStatus = "Faulty",
            Cost = 200000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 10
        },
        new Asset
        {
            AssetId = "AS-003",
            Category = "Medical",
            SubCategory = "X-Ray Machine",
            ItemName = "Digital X-Ray",
            Department = "Radiology",
            Manufacturer = "GE Healthcare",
            SerialNumber = "XR003",
            Model = "Optima XR220amx",
            IsPlacement = true,
            PlacementStartDate = DateTime.Now.AddDays(-60),
            PlacementEndDate = DateTime.Now.AddDays(300),
            Vendor = "GE Healthcare",
            ServiceProvider = "GE",
            ProcurementDate = DateTime.Now.AddDays(-30),
            Status = "New",
            //Location = "Siteki Clinic",
            //FunctionalStatus = "Working",
            Cost = 100000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 8
        },
        new Asset
        {
            AssetId = "AS-004",
            Category = "Non-Medical",
            SubCategory = "Generator",
            ItemName = "Standby Generator",
            Department = "Maintenance",
            Manufacturer = "Caterpillar",
            SerialNumber = "GEN004",
            Model = "C27",
            IsPlacement = true,
            PlacementStartDate = DateTime.Now.AddDays(-90),
            PlacementEndDate = DateTime.Now.AddDays(270),
            Vendor = "Caterpillar",
            ServiceProvider = "Caterpillar",
            ProcurementDate = DateTime.Now.AddDays(-60),
            Status = "Used",
            //Location = "Manzini Hospital",
            //FunctionalStatus = "Working",
            Cost = 30000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 15
        },
        new Asset
        {
            AssetId = "AS-005",
            Category = "Medical",
            SubCategory = "Ultrasound Scanner",
            ItemName = "Portable Ultrasound",
            Department = "Maternity",
            Manufacturer = "Philips",
            SerialNumber = "US005",
            Model = "ClearVue 350",
            IsPlacement = false,
            Vendor = "Philips",
            ServiceProvider = "Philips",
            ProcurementDate = DateTime.Now.AddDays(-90),
            Status = "Refurbished",
            //Location = "Mbabane Hospital",
            //FunctionalStatus = "Working",
            Cost = 15000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 7
        },
        new Asset
        {
            AssetId = "AS-006",
            Category = "Medical",
            SubCategory = "Defibrillator",
            ItemName = "AED",
            Department = "Emergency",
            Manufacturer = "Zoll",
            SerialNumber = "DEF006",
            Model = "AED Plus",
            IsPlacement = true,
            PlacementStartDate = DateTime.Now.AddDays(-120),
            PlacementEndDate = DateTime.Now.AddDays(240),
            Vendor = "Zoll",
            ServiceProvider = "Zoll",
            ProcurementDate = DateTime.Now.AddDays(-120),
            Status = "New",
            //Location = "Lobamba Clinic",
            //FunctionalStatus = "Working",
            Cost = 8000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 10
        },
        new Asset
        {
            AssetId = "AS-007",
            Category = "Non-Medical",
            SubCategory = "Air Conditioner",
            ItemName = "Wall AC Unit",
            Department = "IT Office",
            Manufacturer = "Samsung",
            SerialNumber = "AC007",
            Model = "AR9500T",
            IsPlacement = false,
            Vendor = "Samsung",
            ServiceProvider = "Samsung",
            ProcurementDate = DateTime.Now.AddDays(-150),
            Status = "Used",
            //Location = "Manzini Hospital",
            //FunctionalStatus = "Faulty",
            Cost = 2000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 5
        },
        new Asset
        {
            AssetId = "AS-008",
            Category = "Medical",
            SubCategory = "Oxygen Concentrator",
            ItemName = "O2 Concentrator",
            Department = "ICU",
            Manufacturer = "ResMed",
            SerialNumber = "OC008",
            Model = "Mobi",
            IsPlacement = true,
            PlacementStartDate = DateTime.Now.AddDays(-180),
            PlacementEndDate = DateTime.Now.AddDays(180),
            Vendor = "ResMed",
            ServiceProvider = "ResMed",
            ProcurementDate = DateTime.Now.AddDays(-180),
            Status = "New",
            //Location = "Siteki Clinic",
            //FunctionalStatus = "Working",
            Cost = 10000,
            UnitOfMeasure = "Unit",
            Quantity = 1,
            LifespanPeriod = "years",
            LifespanQuantity = 10
        }
    };
        }
        
        

        public IActionResult Detail(string id)
        {
            var asset = GetAssets().FirstOrDefault(x => x.AssetId == id);
            return View(asset);
        }
        public IActionResult registerAsset()
        {
            Asset Assetmodel = new Asset();
            Assetmodel.AssetId = "AS-" + (GetAssets().Count + 1).ToString("D3");
            //ViewData["assetTag"] = 

            return View(Assetmodel);
        }

        [HttpPost]
        public IActionResult RegisterAsset(Asset Assetmodel)
        {
            //model.AssetId = ViewData["assetTag"].ToString();
            //Assetmodel.AssetId = "AS-" + (GetAssets().Count + 1).ToString("D3");
            Debug.WriteLine($"IsPlacement: {Assetmodel.IsPlacement}");
            Debug.WriteLine($"Category: {Assetmodel.AssetId}");

            return View(Assetmodel);

        }
    }
}
