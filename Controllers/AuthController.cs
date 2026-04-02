using Microsoft.AspNetCore.Mvc;
using Xllerate.Models;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/{controller}")]
public class AuthController : ControllerBase {
    [HttpPost]
    [Route("/register")]
    public IActionResult Register([FromBody] User user) {
        return Ok();
    }
    
    [HttpPost]
    [Route("/login")]
    public IActionResult LoginHandler([FromBody] User user) {
        return Ok();
    }
}