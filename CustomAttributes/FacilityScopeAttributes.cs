using System.Reflection;
using EMMS.Constants;
using EMMS.Models.Admin;
using EMMS.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;

namespace EMMS.CustomAttributes
{
    // Every controller action declares how it keeps users to their own facility's data, with exactly one of:
    //   [FacilityScoped]  - the action takes a record id; the record is checked with FacilityAccess before it runs
    //   [FacilityChecked] - the action scopes itself (a list filtered by the user's facility, or a rule in a service)
    //   [NoFacilityData]  - the action touches nothing that belongs to a facility
    // On a class, the marker covers every action without its own. EMMS.Tests.FacilityScopeCoverageTests enforces it.

    /// <summary>
    /// Checks the record named by an action argument with <see cref="FacilityAccess"/> before the action runs.
    /// <paramref name="parameter"/> is an argument name, or a path into one ("model.MoveAsset.MovementId").
    /// A record that does not exist is left to the action (it reports not found its own way).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class FacilityScopedAttribute : Attribute, IAsyncActionFilter
    {
        public const string DeniedMessage = "You do not have access to that record.";

        public FacilityRecord Record { get; }
        public string Parameter { get; }
        public FacilityAccessLevel Level { get; }

        public FacilityScopedAttribute(FacilityRecord record, string parameter = "id", FacilityAccessLevel level = FacilityAccessLevel.View)
        {
            Record = record;
            Parameter = parameter;
            Level = level;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userJson = context.HttpContext.Session.GetString(Constant.UserSessionString);
            var user = string.IsNullOrWhiteSpace(userJson) ? null : JsonConvert.DeserializeObject<User>(userJson);
            if (user == null)
            {
                // BaseController sends anonymous requests to the login page before this runs; deny if it ever doesn't
                context.Result = new UnauthorizedResult();
                return;
            }

            var id = Resolve(context.ActionArguments, Parameter);
            if (id is null || (id is Guid g && g == Guid.Empty))
            {
                await next();
                return;
            }

            var access = context.HttpContext.RequestServices.GetRequiredService<FacilityAccess>();
            var result = await access.CheckAsync(user, Record, id, Level);
            if (result == AccessResult.Denied)
            {
                context.Result = Denied(context);
                return;
            }
            await next();
        }

        /// <summary>Follows "arg.Prop.Prop" through the bound action arguments; null when any step is missing.</summary>
        public static object? Resolve(IDictionary<string, object?> arguments, string path)
        {
            var parts = path.Split('.');
            if (!arguments.TryGetValue(parts[0], out var value)) return null;
            foreach (var name in parts.Skip(1))
            {
                if (value == null) return null;
                value = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(value);
            }
            return value;
        }

        // Script calls get a plain 404 (no hint the record exists); pages go home with a message.
        private static IActionResult Denied(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            bool script = request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || request.Headers.Accept.Any(a => a != null && a.Contains("application/json"))
                || request.HasJsonContentType();
            if (script) return new NotFoundResult();

            if (context.Controller is Controller controller)
                controller.TempData["Error"] = DeniedMessage;
            return new RedirectToActionResult("Index", "Home", null);
        }
    }

    /// <summary>The action keeps to the user's facility itself; <see cref="How"/> says where.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class FacilityCheckedAttribute : Attribute
    {
        public string How { get; }
        public FacilityCheckedAttribute(string how) => How = how;
    }

    /// <summary>The action reads or writes nothing that belongs to a facility; <see cref="Why"/> says why.</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class NoFacilityDataAttribute : Attribute
    {
        public string Why { get; }
        public NoFacilityDataAttribute(string why) => Why = why;
    }
}
