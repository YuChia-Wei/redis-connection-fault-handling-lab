using Microsoft.AspNetCore.Mvc;

namespace lab.webapi.Controllers;

/// <summary>
/// 首頁轉址
/// </summary>
public class HomeController : Controller
{
    // GET
    public IActionResult Index()
    {
        return new RedirectResult("swagger");
    }
}