using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurrOS.Api.Controllers
{
    [Route("login")]
    public class LoginController : Controller
    {
        private readonly IConfiguration _configuration;

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet("signin")]
        public IActionResult SignIn(string? returnUrl = "/")
        {
            // Validate returnUrl to prevent open redirect attacks
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            if (Uri.IsWellFormedUriString(returnUrl, UriKind.Absolute))
            {
                returnUrl = returnUrl.Trim('/');
            }
            else
            {
                returnUrl = $"{_configuration["BaseUrl"]!.Trim('/')}/{returnUrl.Trim('/')}";
            }

            var props = new AuthenticationProperties
            {
                RedirectUri = returnUrl
            };

            return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [HttpPost("signout")]
        [ValidateAntiForgeryToken]
        public new IActionResult SignOut()
        {
            var props = new AuthenticationProperties
            {
                RedirectUri = "/"
            };

            // Sign out locally (cookie) and at the IdP (OIDC)
            return SignOut(
                props,
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
