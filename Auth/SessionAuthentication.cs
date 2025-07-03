using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using EMMS.Constants;

namespace EMMS.Auth
{
    public class SessionAuthorize : Attribute, IAuthorizationFilter
    {

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.Session.GetString(Constant.UserSessionString);
            var action = context.ActionDescriptor.RouteValues["action"];

            if (string.IsNullOrEmpty(user) &&  action != "Register") {
              
                context.Result = new RedirectToActionResult("Index", "Home", null);
            }
        }
    }
}
