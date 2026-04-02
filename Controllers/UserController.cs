using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Xllerate.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class UserController : ControllerBase {
    [HttpGet]
    [Route("/api/profile")]
    public IActionResult Get() {
        // TODO - Add the service call 
        return Ok();
    }
}