using Microsoft.AspNetCore.Html;
using static EMMS.Models.Enumerators;

namespace EMMS.Utility
{
    public static class Format
    {
        public static string FormatCategory(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return string.Empty;

            const string keyword = "Equipment";

            return category.Replace(keyword, "", StringComparison.OrdinalIgnoreCase).Trim();
        }


        public static IHtmlContent DisplayProcurementStatus(ProcurementStatus procStatus)
        {
             switch (procStatus)
             {
                 case ProcurementStatus.New:
                    return new HtmlString($"<div class='status-btn status-new'>{procStatus}</div>");
                 case ProcurementStatus.Used:
                    return new HtmlString($"<div class='status-btn status-info'>{procStatus}</div>");
                 case ProcurementStatus.Refurbished:
                    return new HtmlString($"<div class='status-btn status-warning'>{procStatus}</div>");
                 case ProcurementStatus.Decommissioned:
                    return new HtmlString($"<div class='status-btn status-danger'>{procStatus}</div>");
                default:
                    return new HtmlString("<div>-</div>");
            }
        }

        public static IHtmlContent DisplayFunctionalStatus(FunctionalStatus? funcStatus)
        {
            switch (funcStatus)
            {
                case FunctionalStatus.Functional:
                    return new HtmlString($"<div class='status-btn status-new'>{funcStatus}</div>");
                case FunctionalStatus.NonFunctional:
                    return new HtmlString($"<div class='status-btn status-danger'>{funcStatus}</div>");
                case FunctionalStatus.UnderMaintenance:
                    return new HtmlString($"<div class='status-btn status-warning'>{funcStatus}</div>");
                case FunctionalStatus.Unknown:
                    return new HtmlString($"<div class='status-btn status-muted'>{funcStatus}</div>");
                default:
                    return new HtmlString("<div>-</div>");
            }
        }
    }
}
