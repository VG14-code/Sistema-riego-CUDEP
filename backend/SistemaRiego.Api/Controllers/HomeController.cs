using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SistemaRiego.Api.Controllers;

[ApiController, ApiExplorerSettings(IgnoreApi = true)]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/"), AllowAnonymous]
    public IActionResult Index() => Redirect("http://127.0.0.1:5173/");
}
