using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using EMMS.Constants;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using EMMS.Utility;
using static EMMS.Models.Enumerators;

public class BaseController : Controller
{
    protected User? CurrentUser { get; private set; }
    protected bool isAdmin { get; private set; }

    // Deny by default: every action needs a signed-in user unless it is marked [AllowAnonymous]
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var userJson = HttpContext.Session.GetString(Constant.UserSessionString);
        if (!string.IsNullOrWhiteSpace(userJson))
        {

            CurrentUser = JsonConvert.DeserializeObject<User>(userJson);
            isAdmin = CurrentUser?.UserRole.UserType == UserType.Administrator;
            ViewData["_currrentUserRole"] = CurrentUser?.UserRole;
            ViewData["_currentUser"] = CurrentUser;
        }

        var allowAnonymous = context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (CurrentUser == null && !allowAnonymous)
        {
            // Script calls get a 401 they can handle; page requests go to the login screen
            context.Result = IsApiOrAjaxRequest(context.HttpContext.Request)
                ? new UnauthorizedResult()
                : new RedirectToActionResult("Login", "Home", null);
            return;
        }

        base.OnActionExecuting(context);
    }

    private static bool IsApiOrAjaxRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        || request.Headers["X-Requested-With"] == "XMLHttpRequest"
        || request.Headers.Accept.Any(a => a != null && a.Contains("application/json"));

    protected void SaveUserSession(User user)
    {
        var serializedObject = JsonConvert.SerializeObject(user);
        HttpContext.Session.SetString(Constant.UserSessionString, serializedObject);
    }

    protected void ClearSession()
    {
        HttpContext.Session.Clear();
    }

    protected void CreateEntity(BaseEntity entity) => EntityStamp.Created(entity, CurrentUser?.UserId);

    protected void UpdateEntity(BaseEntity entity) => EntityStamp.Updated(entity, CurrentUser?.UserId);
}
