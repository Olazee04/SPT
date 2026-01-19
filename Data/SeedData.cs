using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();


        context.Database.EnsureCreated();

        // 1. SEED ROLES
        string[] roles = { "Admin", "Student", "Mentor" };
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

        // 2. SEED ADMIN
        if (await userManager.FindByEmailAsync("admin@spt.com") == null)
        {
            var admin = new ApplicationUser { UserName = "admin", Email = "admin@spt.com", EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // 3. SEED TRACKS
        var tracks = new List<Track>
        {
            new Track { Name = "Fullstack C#", Code = "FSC", Description = "Complete C# & React Path", IsActive = true },
            new Track { Name = "Frontend JavaScript", Code = "FEJ", Description = "Master React & UI", IsActive = true },
            new Track { Name = "Backend Web API", Code = "API", Description = "Master .NET APIs", IsActive = true },
            new Track { Name = "Mobile Game Dev", Code = "MGD", Description = "Unity & C#", IsActive = true }
        };

        foreach (var t in tracks)
        {
            if (!context.Tracks.Any(dbT => dbT.Code == t.Code)) context.Tracks.Add(t);
        }
        await context.SaveChangesAsync();

        // Get Track IDs
        var fsTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "FSC");
        var feTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "FEJ");
        var apiTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "API");
        var mgdTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "MGD");

        // ==========================================
        // 4. SEED MODULES (19 PER COHORT)
        // ==========================================

        // --- A. FULLSTACK (FSC) ---
        if (fsTrack != null && !context.SyllabusModules.Any(m => m.TrackId == fsTrack.Id))
        {
            var modules = new List<SyllabusModule>
            {
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "C#-01", ModuleName = "C# Syntax & Basics", DisplayOrder = 1, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "C#-02", ModuleName = "OOP Fundamentals", DisplayOrder = 2, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "C#-03", ModuleName = "Advanced OOP", DisplayOrder = 3, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "C#-04", ModuleName = "LINQ & Async", DisplayOrder = 4, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "C#-05", ModuleName = "Debugging Techniques", DisplayOrder = 5, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "DB-01", ModuleName = "SQL Database Design", DisplayOrder = 6, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "DB-02", ModuleName = "Advanced SQL", DisplayOrder = 7, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "NET-01", ModuleName = "EF Core Basics", DisplayOrder = 8, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "NET-02", ModuleName = "EF Core Migrations", DisplayOrder = 9, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "WEB-01", ModuleName = "ASP.NET MVC Basics", DisplayOrder = 10, IsActive = true, RequiredHours = 20 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "API-01", ModuleName = "Building REST APIs", DisplayOrder = 11, IsActive = true, RequiredHours = 20 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "API-02", ModuleName = "Authentication (JWT)", DisplayOrder = 12, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "API-03", ModuleName = "Dependency Injection", DisplayOrder = 13, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "API-04", ModuleName = "Middleware", DisplayOrder = 14, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "TST-01", ModuleName = "Unit Testing (xUnit)", DisplayOrder = 15, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "FE-INT", ModuleName = "Connecting React", DisplayOrder = 16, IsActive = true, RequiredHours = 20 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "OPS-01", ModuleName = "Git Version Control", DisplayOrder = 17, IsActive = true, RequiredHours = 10 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "OPS-02", ModuleName = "Docker Basics", DisplayOrder = 18, IsActive = true, RequiredHours = 15 },
                new SyllabusModule { TrackId = fsTrack.Id, ModuleCode = "CAP-FS", ModuleName = "Fullstack Capstone", DisplayOrder = 19, IsActive = true, RequiredHours = 40, HasProject = true }
            };
            context.SyllabusModules.AddRange(modules);
        }


        // --- B. FRONTEND (FEJ) ---
        if (fsTrack != null && !context.SyllabusModules.Any(m => m.TrackId == fsTrack.Id))
        {
            if (feTrack != null)
            {
                var modules = new List<SyllabusModule>();
                // Generate 18 Modules
                for (int i = 1; i <= 18; i++)
                {
                    modules.Add(new SyllabusModule
                    {
                        TrackId = feTrack.Id,
                        ModuleCode = $"FE-{i:00}",
                        ModuleName = $"Frontend Level {i}",
                        DisplayOrder = i,
                        IsActive = true,
                        RequiredHours = 15
                    });
                }
                // Name the important ones
                modules[0].ModuleName = "HTML5 Semantics"; modules[1].ModuleName = "CSS3 Flexbox/Grid";
                modules[2].ModuleName = "JavaScript Basics"; modules[3].ModuleName = "DOM Manipulation";
                modules[4].ModuleName = "React Fundamentals"; modules[5].ModuleName = "React Hooks";

                // Capstone
                modules.Add(new SyllabusModule { TrackId = feTrack.Id, ModuleCode = "CAP-FE", ModuleName = "Frontend Capstone", DisplayOrder = 19, IsActive = true, RequiredHours = 40, HasProject = true });
                context.SyllabusModules.AddRange(modules);
            }

            // --- C. BACKEND API (API) ---
            if (apiTrack != null && !context.SyllabusModules.Any(m => m.TrackId == apiTrack.Id))
            {
                if (apiTrack != null)
                {
                    var modules = new List<SyllabusModule>();
                    for (int i = 1; i <= 18; i++)
                    {
                        modules.Add(new SyllabusModule
                        {
                            TrackId = apiTrack.Id,
                            ModuleCode = $"API-{i:00}",
                            ModuleName = $"Backend Level {i}",
                            DisplayOrder = i,
                            IsActive = true,
                            RequiredHours = 15
                        });
                    }
                    modules[0].ModuleName = "C# Advanced"; modules[1].ModuleName = "SQL Optimization";
                    modules[2].ModuleName = "REST Principles"; modules[3].ModuleName = "Microservices Intro";

                    modules.Add(new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "CAP-API", ModuleName = "Backend Capstone", DisplayOrder = 19, IsActive = true, RequiredHours = 40, HasProject = true });
                    context.SyllabusModules.AddRange(modules);
                }
            }

            // --- D. GAME DEV (MGD) ---
            if (mgdTrack != null && !context.SyllabusModules.Any(m => m.TrackId == mgdTrack.Id))
            {
                var modules = new List<SyllabusModule>();
                for (int i = 1; i <= 18; i++)
                {
                    modules.Add(new SyllabusModule
                    {
                        TrackId = mgdTrack.Id,
                        ModuleCode = $"MGD-{i:00}",
                        ModuleName = $"Unity Level {i}",
                        DisplayOrder = i,
                        IsActive = true,
                        RequiredHours = 15
                    });
                }
                modules[0].ModuleName = "Unity Interface"; modules[1].ModuleName = "C# for Games";
                modules[2].ModuleName = "Physics System"; modules[3].ModuleName = "2D Animation";

                modules.Add(new SyllabusModule { TrackId = mgdTrack.Id, ModuleCode = "CAP-MGD", ModuleName = "Game Capstone", DisplayOrder = 19, IsActive = true, RequiredHours = 40, HasProject = true });
                context.SyllabusModules.AddRange(modules);
            }

            await context.SaveChangesAsync();

            // ==========================================
            // 5. SEED RESOURCES (MATERIALS FOR ALL)
            // ==========================================

            if (!context.Resources.Any())
            {
                int GetModId(string code) => context.SyllabusModules.FirstOrDefault(m => m.ModuleCode == code)?.Id ?? 0;
                var res = new List<Resource>();


                // A. FULLSTACK RESOURCES
                if (fsTrack != null)
                {
                    void AddFS(string code, string title, string url)
                    {
                        int mid = GetModId(code); if (mid > 0) res.Add(new Resource { ModuleId = mid, TrackId = fsTrack.Id, Title = title, Url = url, Type = "Video" });
                    }
                    AddFS("C#-01", "C# Syntax & Basics", "https://www.youtube.com/playlist?list=PL82C6-O4XrHfoN_Y4MwGvJz5BntiL0z0D");
                    AddFS("C#-02", "OOP Fundamentals", "https://www.youtube.com/watch?v=gfkTfcpWqAY");
                    AddFS("DB-01", "SQL Crash Course", "https://www.youtube.com/watch?v=7S_tz1z_5bA");
                    AddFS("NET-01", "EF Core Basics", "https://www.youtube.com/watch?v=dCgYmdk3KjM");
                    AddFS("API-01", "Building REST APIs", "https://www.youtube.com/watch?v=pKd0Rpw7O48");
                    AddFS("CAP-FS", "Capstone Guide", "https://www.youtube.com/watch?v=BfEjDD8mJV2");
                }

                // B. FRONTEND RESOURCES
                if (feTrack != null)
                {
                    for (int i = 1; i <= 18; i++)
                    {
                        int mid = GetModId($"FE-{i:00}");
                        if (mid > 0) res.Add(new Resource { ModuleId = mid, TrackId = feTrack.Id, Title = $"Frontend Video {i}", Url = "https://www.youtube.com/watch?v=G3e-cpL7ofc", Type = "Video" });
                    }
                    int cap = GetModId("CAP-FE");
                    if (cap > 0) res.Add(new Resource { ModuleId = cap, TrackId = feTrack.Id, Title = "Frontend Capstone Ideas", Url = "https://www.youtube.com/watch?v=CgkZ7MvWUAA", Type = "Video" });
                }

                // C. API RESOURCES
                if (apiTrack != null)
                {
                    for (int i = 1; i <= 18; i++)
                    {
                        int mid = GetModId($"API-{i:00}");
                        if (mid > 0) res.Add(new Resource { ModuleId = mid, TrackId = apiTrack.Id, Title = $"Backend Video {i}", Url = "https://www.youtube.com/watch?v=pKd0Rpw7O48", Type = "Video" });
                    }
                    int cap = GetModId("CAP-API");
                    if (cap > 0) res.Add(new Resource { ModuleId = cap, TrackId = apiTrack.Id, Title = "Full API Build", Url = "https://www.youtube.com/watch?v=4N4p16dgoXw", Type = "Video" });
                }

                // D. GAME DEV RESOURCES
                if (mgdTrack != null)
                {
                    for (int i = 1; i <= 18; i++)
                    {
                        int mid = GetModId($"MGD-{i:00}");
                        if (mid > 0) res.Add(new Resource { ModuleId = mid, TrackId = mgdTrack.Id, Title = $"Unity Tutorial {i}", Url = "https://www.youtube.com/watch?v=nPW6tKeapsM", Type = "Video" });
                    }
                    int cap = GetModId("CAP-MGD");
                    if (cap > 0) res.Add(new Resource { ModuleId = cap, TrackId = mgdTrack.Id, Title = "Game Dev Capstone", Url = "https://www.youtube.com/watch?v=j48LtUkZRjU", Type = "Video" });
                }

                if (res.Any())
                {
                    context.Resources.AddRange(res);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}