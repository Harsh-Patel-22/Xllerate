using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Xllerate.Models;
using Xllerate.Models.DTOs.Auth;
using Xllerate.Services;

namespace Xllerate.Controllers;

[ApiController]
[Route("api/{controller}")]
public class AuthController(UserManager<User> _userManager, SignInManager<User> _signInManager, JwtService _jwtService) : ControllerBase {
    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> RegisterHandler([FromBody] RegisterRequest request) {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return Conflict(new { message = "Email already registered." });

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        // Generate and return token immediately — user is logged in after register
        var token = _jwtService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }
    
    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> LoginHandler([FromBody] LoginRequest request) {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        // CheckPasswordSignInAsync handles lockout automatically
        var result = await _signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Unauthorized(new
            {
                message = "Account locked after too many failed attempts. Try again in 5 minutes."
            });

        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _jwtService.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }
}