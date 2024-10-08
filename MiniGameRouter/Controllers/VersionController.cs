using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class VersionController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");
    }
}