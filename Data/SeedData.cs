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

        // =============================
        // DATABASE (SAFE FOR IDENTITY)
        // =============================
        await context.Database.MigrateAsync();

        // ==========
        // 1. ROLES
        // ==========
        string[] roles = { "Admin", "Student", "Mentor" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // ==============
        // 2. ADMIN USER
        // ==============
        if (await userManager.FindByEmailAsync("admin@spt.com") == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@spt.com",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // ============
        // 3. TRACKS
        // ============
        if (!await context.Tracks.AnyAsync())
        {
            context.Tracks.AddRange(
                new Track { Name = "Frontend JavaScript", Code = "FEJ", IsActive = true },
                new Track { Name = "Backend C#", Code = "BEC", IsActive = true },
                new Track { Name = "Fullstack", Code = "FSC", IsActive = true },
                new Track { Name = "Backend Web API Development", Code = "API", IsActive = true },
                new Track { Name = "Mobile Game Development", Code = "MGD", IsActive = true },
                new Track { Name = "Web 3", Code = "WB3", IsActive = true }
            );

            await context.SaveChangesAsync();
        }

        // ===================================
        // 4. ENSURE ALL STUDENTS HAVE TRACK
        // ===================================
        var defaultTrack = await context.Tracks
            .AsNoTracking()
            .FirstAsync(t => t.Code == "FSC");

        var studentsWithoutTrack = await context.Students
            .Where(s => s.TrackId == null)
            .ToListAsync();

        if (studentsWithoutTrack.Any())
        {
            foreach (var s in studentsWithoutTrack)
            {
                s.TrackId = defaultTrack.Id;
            }

            await context.SaveChangesAsync();
        }

        // ==========================
        // 5. MODULES — 19 PER TRACK
        // ==========================
        var tracks = await context.Tracks
            .AsNoTracking()
            .ToListAsync();

        foreach (var track in tracks)
        {
            bool hasModules = await context.SyllabusModules
                .AsNoTracking()
                .AnyAsync(m => m.TrackId == track.Id);

            if (hasModules)
                continue;

            var modules = new List<SyllabusModule>();

            // MODULES 1–18 (LEARNING)
            for (int i = 1; i <= 18; i++)
            {
                modules.Add(new SyllabusModule
                {
                    TrackId = track.Id,
                    DisplayOrder = i,
                    ModuleCode = $"{track.Code}-{i:00}",
                    ModuleName = $"{track.Name} – Module {i}",
                    RequiredHours = 8,
                    DifficultyLevel = i <= 5 ? "Beginner" : i <= 12 ? "Intermediate" : "Advanced",
                    Topics = $"Core learning content for {track.Name} (Part {i})",
                    HasQuiz = i % 3 == 0,
                    IsActive = true
                });
            }

            // MODULE 19 (MINI PROJECT)
            modules.Add(new SyllabusModule
            {
                TrackId = track.Id,
                DisplayOrder = 19,
                ModuleCode = $"CAP-{track.Code}",
                ModuleName = "Mini Project",
                RequiredHours = 40,
                DifficultyLevel = "Expert",
                Topics = $"Build a real-world {track.Name} project",
                HasProject = true,
                IsMiniProject = true,
                IsActive = true
            });

            context.SyllabusModules.AddRange(modules);
            await context.SaveChangesAsync();
        }
        // =====================================================
        // 6. MODULE RESOURCES – NORMALIZE TO 2 PER MODULE
        // =====================================================
        var learningModules = await context.SyllabusModules
            .Where(m => m.DisplayOrder <= 18)
            .Include(m => m.Resources)
             .Include(m => m.Track)
            .ToListAsync();

        foreach (var module in learningModules)
        {
            var resources = await context.ModuleResources
                .Where(r => r.ModuleId == module.Id)
                .OrderBy(r => r.Id)
                .ToListAsync();

            // Ensure Documentation resource
            var doc = resources.FirstOrDefault(r => r.Type == "Article");
            if (doc == null)
            {
                context.ModuleResources.Add(new ModuleResource
                {
                    ModuleId = module.Id,
                    Title = $"{module.ModuleName} – Official Documentation",
                    Url = GetDocumentationUrl(module),
                    Type = "Article",
                    IsActive = true
                });
            }
            else
            {
                doc.Title = $"{module.ModuleName} – Official Documentation";
                doc.Url = GetDocumentationUrl(module);
            }

            // Ensure Video resource
            var video = resources.FirstOrDefault(r => r.Type == "Video");
            if (video == null)
            {
                context.ModuleResources.Add(new ModuleResource
                {
                    ModuleId = module.Id,
                    Title = $"{module.ModuleName} – Video Tutorial",
                    Url = GetVideoUrl(module),
                    Type = "Video",
                    IsActive = true
                });
            }
            else
            {
                video.Title = $"{module.ModuleName} – Video Tutorial";
                video.Url = GetVideoUrl(module);
            }

           
            foreach (var extra in resources.Skip(2))
                context.ModuleResources.Remove(extra);
        }

        await context.SaveChangesAsync();



        // ===================================
        // 7. TRACK-LEVEL RESOURCES (LIBRARY)
        // ====================================
        if (!await context.Resources.AnyAsync())
        {
            var allTracks = await context.Tracks
                .AsNoTracking()
                .ToListAsync();

            foreach (var track in allTracks)
            {
                context.Resources.Add(new Resource
                {
                    TrackId = track.Id,
                    Title = $"{track.Name} – Official Resources",
                    Description = "Curated documentation and tutorials",
                    Url = "https://learn.microsoft.com/",
                    Type = "Link",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }
    }

    // ===================================
    // DEFAULT RESOURCE URLS (SAFE & REAL)
    // ====================================
    private static string GetDefaultResourceUrl(SyllabusModule module)
    {
        return module.Track.Code switch
        {
            "FSC" => "https://learn.microsoft.com/en-us/dotnet/",
            "API" => "https://learn.microsoft.com/en-us/aspnet/core/web-api/",
            "FEJ" => "https://developer.mozilla.org/en-US/docs/Web/JavaScript",
            "BEC" => "https://learn.microsoft.com/en-us/dotnet/csharp/",
            "MGD" => "https://learn.unity.com/",
            "WB3" => "https://docs.soliditylang.org/",
            _ => "https://learn.microsoft.com/"
        };
    }

    private static string GetDocumentationUrl(SyllabusModule module)
    {
        if (module == null || module.Track == null)
        {
            return "#";
        }
        return module.Track.Code switch
        {
            "API" => "https://learn.microsoft.com/en-us/aspnet/core/web-api/",
            "FSC" => "https://learn.microsoft.com/en-us/dotnet/",
            "FEJ" => "https://developer.mozilla.org/en-US/docs/Web/JavaScript",
            "BEC" => "https://learn.microsoft.com/en-us/dotnet/csharp/",
            "MGD" => "https://learn.unity.com/",
            "WB3" => "https://docs.soliditylang.org/",
            _ => "https://learn.microsoft.com/"
        };
    }

    private static string GetVideoUrl(SyllabusModule module)
    {
        return module.Track.Code switch
        {
            "API" => "https://www.youtube.com/watch?v=pKd0Rpw7O48",
            "FSC" => "https://www.youtube.com/watch?v=gfkTfcpWqAY",
            "FEJ" => "https://www.youtube.com/watch?v=PkZNo7MFNFg",
            "BEC" => "https://www.youtube.com/watch?v=GhQdlIFylQ8",
            "MGD" => "https://www.youtube.com/watch?v=gB1F9G0JXOo",
            "WB3" => "https://www.youtube.com/watch?v=gyMwXuJrbJQ",
            _ => "https://www.youtube.com/"
        };
    }


}
