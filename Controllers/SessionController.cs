using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using EMMS.Constants;
using EMMS.Models.Admin;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EMMS.Controllers
{
    public class SessionController: Controller
    {
        public void SaveUserSession(User user)
        {
            var serializedObject = JsonConvert.SerializeObject(user);
            HttpContext.Session.SetString(Constant.UserSessionString, serializedObject);
        }

        public User? GetCurrentSessionUser()
        {
            var userJson = HttpContext.Session.GetString(Constant.UserSessionString);

            if (string.IsNullOrWhiteSpace(userJson))
                return null;

            var currentUser = JsonConvert.DeserializeObject<User>(userJson);

            ViewData["_currrentUserRole"] = currentUser?.UserRole;

            return currentUser;
        }

        public void ClearSession()
        {
            HttpContext.Session.Clear();
        }
    }
}
