using Microsoft.AspNetCore.Mvc;

namespace Xllerate.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class TestingController : ControllerBase{
    
    public IActionResult Get() {
        return Ok("Hello World!");
    }
}