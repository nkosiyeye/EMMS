using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using static EMMS.Models.Enumerators;

namespace EMMS.ViewModels
{
    // Colour of a value. Neutral is the default; the others are used only when the value needs attention.
    public enum DashTone : byte
    {
        Neutral = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Danger = 4,
    }

    public static class DashToneExtensions
    {
        /// <summary>Bootstrap colour name for the tone, used as text-{x} / bg-{x}.</summary>
        public static string Css(this DashTone tone) => tone switch
        {
            DashTone.Info => "primary",
            DashTone.Success => "success",
            DashTone.Warning => "warning",
            DashTone.Danger => "danger",
            _ => "secondary",
        };
    }

    /// <summary>
    /// An MVC route plus optional query values (rendered with Url.Dash). With no controller it is an in-page
    /// anchor (#Fragment), so the current query string, e.g. the admin's facility filter, is kept.
    /// </summary>
    public class DashLink
    {
        public string Controller { get; set; } = "";
        public string Action { get; set; } = "Index";
        public string? Id { get; set; }
        public Dictionary<string, string?> Query { get; set; } = new();
        public string? Fragment { get; set; }

        public DashLink() { }
        public DashLink(string controller, string action = "Index", object? id = null)
        {
            Controller = controller;
            Action = action;
            Id = id?.ToString();
        }

        public static DashLink Anchor(string fragment) => new() { Controller = "", Fragment = fragment };

        /// <summary>Adds a query value; null or empty values are left out of the URL.</summary>
        public DashLink With(string key, object? value)
        {
            Query[key] = value?.ToString();
            return this;
        }

        /// <summary>
        /// Opens a list page on one row: the page scrolls to and highlights the element with data-focus-id = id
        /// and, with act, clicks that row's [data-focus-act] button (site.js, emmsFocusRow).
        /// </summary>
        public DashLink Focus(object id, string? act = null) => With("focus", id).With("act", act);
    }

    public static class DashLinkUrlExtensions
    {
        public static string Dash(this IUrlHelper url, DashLink link)
        {
            if (string.IsNullOrEmpty(link.Controller))
                return "#" + link.Fragment;

            var values = new RouteValueDictionary();
            if (!string.IsNullOrEmpty(link.Id)) values["id"] = link.Id;
            foreach (var (key, value) in link.Query)
                if (!string.IsNullOrEmpty(value)) values[key] = value;

            return url.Action(link.Action, link.Controller, values, protocol: null, host: null, fragment: link.Fragment) ?? "#";
        }
    }

    /// <summary>One headline number. The whole tile links to the list it counts.</summary>
    public class StatTile
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "0";
        public string? Context { get; set; }
        public string Icon { get; set; } = "";   // Font Awesome class, e.g. "fa-solid fa-box-open"
        public DashTone Tone { get; set; } = DashTone.Neutral;
        public DashLink? Link { get; set; }
    }

    /// <summary>A one-line banner shown only when something needs doing (e.g. accounts waiting for activation).</summary>
    public class DashNotice
    {
        public string Text { get; set; } = "";
        public string LinkText { get; set; } = "";
        public DashLink Link { get; set; } = new();
    }

    public class BreakdownRow
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public DashTone Tone { get; set; } = DashTone.Info;
        public DashLink? Link { get; set; }     // clicking the bar opens the asset list filtered to this row
    }

    /// <summary>A short ranked list drawn as a horizontal bar chart. Bar colour comes from each row's tone.</summary>
    public class Breakdown
    {
        public string Title { get; set; } = "";
        public List<BreakdownRow> Rows { get; set; } = new();
        public string EmptyText { get; set; } = "Nothing to show.";
        public string? More { get; set; }       // e.g. "+3 more facilities (5 items)" when Rows was cut short
    }

    public class TableCell
    {
        public string Text { get; set; } = "";
        public string? Sub { get; set; }
        public DashTone SubTone { get; set; } = DashTone.Neutral;
        public DashLink? Link { get; set; }
    }

    /// <summary>
    /// The icon button at the end of a row (same style as the app's other tables): a link, the global
    /// service-request form, or a plain status label when there is nothing to do.
    /// </summary>
    public class RowAction
    {
        public string Label { get; set; } = "";              // button tooltip, or the badge text when IsLabel
        public string Icon { get; set; } = "fa-solid fa-eye";
        public string Style { get; set; } = "info";         // Bootstrap button colour: btn-{Style}
        public DashLink? Link { get; set; }
        public Guid? ServiceRequestAssetId { get; set; }   // posts the layout's #serviceRequestForm
        public bool IsLabel => Link == null && ServiceRequestAssetId == null;
    }

    public class TableRow
    {
        public List<TableCell> Cells { get; set; } = new();
        public RowAction? Action { get; set; }
    }

    /// <summary>The role's work queue.</summary>
    public class WorkTable
    {
        public string Title { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public List<TableRow> Rows { get; set; } = new();
        public string EmptyText { get; set; } = "Nothing waiting.";
        public DashLink? ViewAll { get; set; }
        public string? Anchor { get; set; }     // element id, so a tile can link to the table (DashLink.Anchor)
    }

    public class HomeDashboardViewModel
    {
        public UserType Role { get; set; }
        public int? FacilityId { get; set; }     // null = all facilities (admin)
        public List<DashNotice> Notices { get; set; } = new();
        public List<StatTile> Stats { get; set; } = new();
        public List<Breakdown> Breakdowns { get; set; } = new();
        public List<WorkTable> Tables { get; set; } = new();
    }
}
