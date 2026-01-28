using Microsoft.AspNetCore.Mvc;

namespace FIS.Web.Controllers;

/// <summary>
/// Account controller for handling authentication actions
/// </summary>
public class AccountController : Controller
{
    /// <summary>
    /// Logout endpoint that accepts POST/GET and redirects to Microsoft SignOut
    /// </summary>
    [HttpPost]
    [HttpGet]
    [Route("/Account/Logout")]
    [IgnoreAntiforgeryToken]
    public IActionResult Logout()
    {
        // Redirect to Microsoft Identity SignOut with GET request
        return Redirect("/MicrosoftIdentity/Account/SignOut");
    }
}
