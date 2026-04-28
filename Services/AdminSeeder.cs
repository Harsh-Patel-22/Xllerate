using Microsoft.AspNetCore.Identity;
using Xllerate.Models;

namespace Xllerate.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        // Ensure Admin role exists
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole<Guid>("Admin"));

        var email    = config["AdminSeed:Email"]!;
        var password = config["AdminSeed:Password"]!;
        var fullName = config["AdminSeed:FullName"] ?? "Admin";

        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null) return; // already seeded

        var admin = new User
        {
            UserName  = email,
            Email     = email,
            FullName  = fullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Admin seeding failed: {errors}");
        }
    }
}