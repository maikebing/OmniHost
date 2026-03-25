using Microsoft.AspNetCore.Mvc;
using OmniHost.Sample.WebShowcase.Models;

namespace OmniHost.Sample.WebShowcase.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/mvc")]
    public IActionResult Index()
    {
        var model = new MvcDashboardViewModel(
            "ASP.NET MVC Core Example",
            "Classic controller + Razor view rendering, hosted inside an OmniHost window.",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            new[]
            {
                "Server-rendered HTML from a controller action",
                "Typed model passed into a Razor View",
                "Good fit when the shell should stay mostly SSR"
            });

        return View("Index", model);
    }
}
