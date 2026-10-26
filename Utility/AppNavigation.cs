using static EMMS.Models.Enumerators;

namespace EMMS.Utility
{
    /// <summary>
    /// One entry of the main menu: a link (Controller + Action) or a collapsible section of links (Children).
    /// A link is the current page for the routes in <see cref="Matches"/> ("Controller/action" or "Controller/*");
    /// a section can also own pages that none of its links do (e.g. job cards under Job Management).
    /// </summary>
    public sealed class NavItem
    {
        public required string Label { get; init; }
        public string? Icon { get; init; }
        public string? Controller { get; init; }
        public string? Action { get; init; }
        public NavItem[] Children { get; init; } = [];
        public string[] Matches { get; init; } = [];
        public Func<UserType, bool> VisibleTo { get; init; } = _ => true;

        /// <summary>Small heading shown above this entry in the sidebar (starts a group, e.g. the admin links).</summary>
        public string? Heading { get; init; }

        public bool IsSection => Children.Length > 0;

        /// <summary>Element id of a section's collapsible list.</summary>
        public string CollapseId => "nav-" + new string(Label.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>Where the current page sits in the menu: its section and link, either of which may be missing.</summary>
    public sealed record NavLocation(NavItem? Section, NavItem? Link);

    /// <summary>
    /// The app's main menu, defined once. _SidebarContent renders it and _Breadcrumb uses it to show where the
    /// current page sits, so a new page only needs a route in Matches (not a ViewData flag) to be placed.
    /// </summary>
    public static class AppNavigation
    {
        private static bool NotDataCollector(UserType role) => role != UserType.DataCollector;

        public static readonly NavItem[] Items =
        [
            new() { Label = "Dashboard", Icon = "fa-gauge", Controller = "Home", Action = "Index", Matches = ["Home/Index"] },
            new() { Label = "Asset Management", Icon = "fa-box-open", Controller = "AssetManagement", Action = "Index", Matches = ["AssetManagement/*"] },
            new()
            {
                Label = "Asset Movement", Icon = "fa-arrows-turn-to-dots", VisibleTo = NotDataCollector,
                Children =
                [
                    new() { Label = "Move Asset", Controller = "AssetMovement", Action = "Index", Matches = ["AssetMovement/*"] },
                    new() { Label = "Movement Queue", Controller = "AssetMovement", Action = "Queue", Matches = ["AssetMovement/Queue"] },
                    new() { Label = "Receive Asset", Controller = "AssetMovement", Action = "recieveAsset", Matches = ["AssetMovement/recieveAsset"] },
                ]
            },
            new()
            {
                Label = "Job Management", Icon = "fa-toolbox", VisibleTo = NotDataCollector,
                Matches = ["JobManagement/*"],
                Children =
                [
                    new()
                    {
                        Label = "Work Requests", Controller = "JobManagement", Action = "Index",
                        Matches = ["JobManagement/Index", "JobManagement/workRequest", "JobManagement/editWorkRequest", "JobManagement/infrustructureWorkRequest"]
                    },
                    new() { Label = "Service Schedule", Controller = "ServiceSchedule", Action = "Index", Matches = ["ServiceSchedule/*"] },
                    new()
                    {
                        Label = "Create Job", Controller = "JobManagement", Action = "manageJobs", Matches = ["JobManagement/manageJobs"],
                        VisibleTo = role => role is UserType.Biomed or UserType.Administrator
                    },
                    new()
                    {
                        Label = "Technician Workload", Controller = "JobManagement", Action = "workload", Matches = ["JobManagement/workload"],
                        VisibleTo = role => role is UserType.Administrator or UserType.FacilityManager
                    },
                ]
            },
            new() { Label = "Cylinder Requests", Icon = "fa-prescription-bottle-medical", Controller = "Cylinder", Action = "Index", Matches = ["Cylinder/*"], VisibleTo = NotDataCollector },
            new() { Label = "Cylinder Dashboard", Icon = "fa-chart-bar", Controller = "Cylinder", Action = "Dashboard", Matches = ["Cylinder/Dashboard"], VisibleTo = NotDataCollector },
            new() { Label = "Reports", Icon = "fa-file-excel", Controller = "Reports", Action = "Index", Matches = ["Reports/*"], VisibleTo = NotDataCollector },
            new() { Label = "Notifications", Icon = "fa-bell", Controller = "Notification", Action = "Index", Matches = ["Notification/*"], VisibleTo = NotDataCollector },
            new()
            {
                Label = "Lookup List", Icon = "fa-list", Controller = "Admin", Action = "LookupList", Heading = "Admin Section",
                Matches = ["Admin/LookupList", "Admin/AddLookupItem", "Admin/EditLookupItem"],
                VisibleTo = role => role == UserType.Administrator
            },
            new()
            {
                Label = "User Management", Icon = "fa-users", VisibleTo = role => role == UserType.Administrator,
                Children =
                [
                    new() { Label = "User Accounts", Controller = "Users", Action = "Index", Matches = ["Users/*"] },
                    new() { Label = "User Roles", Controller = "Admin", Action = "UserRoles", Matches = ["Admin/UserRoles", "Admin/createUserRole"] },
                ]
            },
        ];

        /// <summary>Finds the current page in the menu. An exact action match wins over a "Controller/*" one.</summary>
        public static NavLocation Locate(string? controller, string? action)
        {
            NavLocation best = new(null, null);
            int bestScore = 0;

            void Consider(NavItem item, NavItem? section, NavItem? link)
            {
                int score = item.Matches.Select(pattern => Score(pattern, controller, action)).DefaultIfEmpty(0).Max();
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new NavLocation(section, link);
                }
            }

            foreach (var item in Items)
            {
                if (!item.IsSection)
                {
                    Consider(item, null, item);
                    continue;
                }
                foreach (var child in item.Children)
                    Consider(child, item, child);
                // Considered after its links, so on an equal score a link keeps the page
                Consider(item, item, null);
            }
            return best;
        }

        // 2 = this exact action, 1 = any action of this controller, 0 = no match
        private static int Score(string pattern, string? controller, string? action)
        {
            var parts = pattern.Split('/');
            if (!string.Equals(parts[0], controller, StringComparison.OrdinalIgnoreCase)) return 0;
            if (parts[1] == "*") return 1;
            return string.Equals(parts[1], action, StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }
    }
}
