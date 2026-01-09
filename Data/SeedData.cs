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

        // 6. Seed Syllabus Modules (Only if empty)
        if (!context.SyllabusModules.Any())
        {
            // Fetch Tracks to get their IDs
            var backendTrack = context.Tracks.FirstOrDefault(t => t.Code == "BEC"); // Backend C#
            var frontendTrack = context.Tracks.FirstOrDefault(t => t.Code == "FEJ"); // Frontend JS
            var fullstackTrack = context.Tracks.FirstOrDefault(t => t.Code == "FSC"); // Fullstack

            var modules = new List<SyllabusModule>();

            // --- BACKEND C# MODULES ---
            if (backendTrack != null)
            {
                modules.AddRange(new[]
                {
                    new SyllabusModule { ModuleCode = "C#101", ModuleName = "Introduction to C# & .NET", TrackId = backendTrack.Id, RequiredHours = 20, DisplayOrder = 1 },
                    new SyllabusModule { ModuleCode = "C#102", ModuleName = "Control Structures & Loops", TrackId = backendTrack.Id, RequiredHours = 15, DisplayOrder = 2 },
                    new SyllabusModule { ModuleCode = "C#103", ModuleName = "Object-Oriented Programming (OOP)", TrackId = backendTrack.Id, RequiredHours = 30, DisplayOrder = 3, HasQuiz = true },
                    new SyllabusModule { ModuleCode = "C#104", ModuleName = "LINQ & Collections", TrackId = backendTrack.Id, RequiredHours = 20, DisplayOrder = 4 },
                    new SyllabusModule { ModuleCode = "NET201", ModuleName = "ASP.NET Core MVC Basics", TrackId = backendTrack.Id, RequiredHours = 40, DisplayOrder = 5, HasProject = true }
                });
            }

            // --- FRONTEND JS MODULES ---
            if (frontendTrack != null)
            {
                modules.AddRange(new[]
                {
                    new SyllabusModule { ModuleCode = "WEB101", ModuleName = "HTML5 & CSS3 Mastery", TrackId = frontendTrack.Id, RequiredHours = 25, DisplayOrder = 1 },
                    new SyllabusModule { ModuleCode = "JS101", ModuleName = "JavaScript Basics (ES6+)", TrackId = frontendTrack.Id, RequiredHours = 30, DisplayOrder = 2, HasQuiz = true },
                    new SyllabusModule { ModuleCode = "JS102", ModuleName = "DOM Manipulation & Events", TrackId = frontendTrack.Id, RequiredHours = 20, DisplayOrder = 3 },
                    new SyllabusModule { ModuleCode = "RCT201", ModuleName = "React.js Fundamentals", TrackId = frontendTrack.Id, RequiredHours = 45, DisplayOrder = 4, HasProject = true }
                });
            }

            // --- FULLSTACK MODULES (Mix of both) ---
            if (fullstackTrack != null)
            {
                modules.AddRange(new[]
                {
                    new SyllabusModule { ModuleCode = "FS101", ModuleName = "Web Fundamentals (HTML/CSS)", TrackId = fullstackTrack.Id, RequiredHours = 20, DisplayOrder = 1 },
                    new SyllabusModule { ModuleCode = "FS102", ModuleName = "C# Programming Basics", TrackId = fullstackTrack.Id, RequiredHours = 25, DisplayOrder = 2 },
                    new SyllabusModule { ModuleCode = "FS103", ModuleName = "Database Design (SQL)", TrackId = fullstackTrack.Id, RequiredHours = 20, DisplayOrder = 3 },
                    new SyllabusModule { ModuleCode = "FS104", ModuleName = "Building APIs with .NET", TrackId = fullstackTrack.Id, RequiredHours = 35, DisplayOrder = 4, HasProject = true }
                });
            }

            context.SyllabusModules.AddRange(modules);
            await context.SaveChangesAsync();
        }
    }
}