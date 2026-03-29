using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NewShadowGuard.Attributes
{
    public class CustomAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        // Конструктор без параметров - требует просто авторизации
        public CustomAuthorizeAttribute()
        {
            _allowedRoles = null;
        }

        // Конструктор с ролями - требует конкретную роль
        public CustomAuthorizeAttribute(params string[] roles)
        {
            _allowedRoles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userId = context.HttpContext.Session.GetString("UserId");
            var role = context.HttpContext.Session.GetString("Role");

            // Проверка: пользователь авторизован?
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // Проверка: есть ли нужная роль?
            if (_allowedRoles != null && !string.IsNullOrEmpty(role))
            {
                bool hasAccess = false;
                foreach (var allowedRole in _allowedRoles)
                {
                    if (role.Equals(allowedRole, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAccess = true;
                        break;
                    }
                }

                if (!hasAccess)
                {
                    context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                    return;
                }
            }
        }
    }
}