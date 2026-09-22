using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace ChurrOS.Api.Utils.AspNet
{
    public sealed class FormValueRequiredAttribute : ActionMethodSelectorAttribute
    {
        private readonly string _name;
        private readonly string? _excludedName;

        public FormValueRequiredAttribute(string name, string? excludedName = null)
        {
            _name = name;
            _excludedName = excludedName;
        }

        public override bool IsValidForRequest(RouteContext context, ActionDescriptor action)
        {
            if (string.Equals(context.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(context.HttpContext.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(context.HttpContext.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(context.HttpContext.Request.Method, "TRACE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.HttpContext.Request.ContentType))
            {
                return false;
            }

            if (!context.HttpContext.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrEmpty(context.HttpContext.Request.Form[_name]))
            {
                return false;
            }

            // Without this, a form carrying both submit.Accept and submit.Deny (a malformed or
            // adversarial POST -- the real consent form never emits both) makes both actions'
            // IsValidForRequest return true, and ASP.NET Core's action selector throws
            // AmbiguousMatchException for two equally-valid candidates on the same route: an
            // unhandled 500 instead of either well-defined outcome. Excluding here means neither
            // matches, so the request falls through to the unconstrained Authorize() action
            // instead -- treated as a plain (re-)prompt, not a decision.
            if (_excludedName is not null && !string.IsNullOrEmpty(context.HttpContext.Request.Form[_excludedName]))
            {
                return false;
            }

            return true;
        }
    }
}
