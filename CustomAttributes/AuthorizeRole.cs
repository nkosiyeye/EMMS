namespace EMMS.CustomAttributes
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Newtonsoft.Json;
    using EMMS.Constants;
    using EMMS.Models.Admin;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class AuthorizeRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public AuthorizeRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var userJson = session.GetString(Constant.UserSessionString);

            if (string.IsNullOrWhiteSpace(userJson))
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
                return;
            }

            var user = JsonConvert.DeserializeObject<User>(userJson);

            if (user?.UserRole == null || !_roles.Contains(user.UserRole.UserType.ToString()))
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
            }
        }
    }

}
