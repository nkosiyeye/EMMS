using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using EMMS.Constants;
using EMMS.Models.Admin;
using EMMS.Models.Domain;
using static EMMS.Models.Enumerators;

public class BaseController : Controller
{
    protected User? CurrentUser { get; private set; }
    protected bool isAdmin { get; private set; }

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

        base.OnActionExecuting(context);
    }

    protected void SaveUserSession(User user)
    {
        var serializedObject = JsonConvert.SerializeObject(user);
        HttpContext.Session.SetString(Constant.UserSessionString, serializedObject);
    }

    protected void ClearSession()
    {
        HttpContext.Session.Clear();
    }

    protected void CreateEntity(BaseEntity entity)
    {
        entity.CreatedBy = CurrentUser?.UserId;
        entity.DateCreated = DateTime.Now;
        entity.RowState = RowStatus.Active;
    }

    protected void UpdateEntity(BaseEntity entity)
    {
        entity.ModifiedBy = CurrentUser?.UserId;
        entity.DateModified = DateTime.Now;
        entity.RowState = RowStatus.Active;
    }
}
