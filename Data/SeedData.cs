using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // 1. Ensure Database is Created
        context.Database.EnsureCreated();

        // 2. Seed Roles
        string[] roles = { "Admin", "Student", "Mentor" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // 3. Seed Admin User
        if (await userManager.FindByEmailAsync("admin@spt.com") == null)
        {
            var admin = new ApplicationUser { UserName = "admin", Email = "admin@spt.com", EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // 4. Seed Tracks (Specific Requirements)
        if (!context.Tracks.Any())
        {
            context.Tracks.AddRange(
                new Track { Name = "Frontend JavaScript", Code = "FEJ" },
                new Track { Name = "Backend C#", Code = "BEC" },
                new Track { Name = "Fullstack", Code = "FSC" },
                new Track { Name = "API Integration & Development", Code = "API" },
                new Track { Name = "Mobile Game Development", Code = "MGD" },
                new Track { Name = "Web 3", Code = "WB3" }
            );
            await context.SaveChangesAsync();
        }

        // 5. Seed Mentors (Mr Azeez & Mr Taofeek)
        if (!context.Mentors.Any())
        {
            // Create Mentor Users first
            var m1User = new ApplicationUser { UserName = "azeez", Email = "azeez@spt.com", EmailConfirmed = true };
            var m2User = new ApplicationUser { UserName = "taofeek", Email = "taofeek@spt.com", EmailConfirmed = true };

            await userManager.CreateAsync(m1User, "Mentor@123");
            await userManager.CreateAsync(m2User, "Mentor@123");
            await userManager.AddToRoleAsync(m1User, "Mentor");
            await userManager.AddToRoleAsync(m2User, "Mentor");

            // Create Mentor Profiles
            context.Mentors.AddRange(
                new Mentor { UserId = m1User.Id, FullName = "Mr. Azeez" },
                new Mentor { UserId = m2User.Id, FullName = "Mr. Taofeek" }
            );
            await context.SaveChangesAsync();
        }
    }
}