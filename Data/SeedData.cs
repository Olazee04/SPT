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

        // ==========================================
        // 1. ENSURE DATABASE & ROLES
        // ==========================================
        context.Database.EnsureCreated();

        string[] roles = { "Admin", "Student", "Mentor" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ==========================================
        // 2. SEED ADMIN USER
        // ==========================================
        if (await userManager.FindByEmailAsync("admin@spt.com") == null)
        {
            var admin = new ApplicationUser { UserName = "admin", Email = "admin@spt.com", EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // ==========================================
        // 3. SEED TRACKS
        // ==========================================
        if (!context.Tracks.Any())
        {
            context.Tracks.AddRange(
                new Track { Name = "Frontend JavaScript", Code = "FEJ" },
                new Track { Name = "Backend C#", Code = "BEC" },
                new Track { Name = "Fullstack", Code = "FSC" },
                new Track { Name = "Backend Web API Development", Code = "API" },
                new Track { Name = "Mobile Game Development", Code = "MGD" },
                new Track { Name = "Web 3", Code = "WB3" }
            );
            await context.SaveChangesAsync();
        }

        // Ensure API Track exists if it was added later
        var apiTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "API");
        if (apiTrack == null)
        {
            apiTrack = new Track
            {
                Name = "Backend Web API Development",
                Code = "API",
                Description = "Master C#, .NET Core, and RESTful APIs."
            };
            context.Tracks.Add(apiTrack);
            await context.SaveChangesAsync();
        }

        // ==========================================
        // 4. SEED MENTORS
        // ==========================================
        if (!context.Mentors.Any())
        {
            var m1User = new ApplicationUser { UserName = "azeez", Email = "azeez@spt.com", EmailConfirmed = true };
            var m2User = new ApplicationUser { UserName = "taofeek", Email = "taofeek@spt.com", EmailConfirmed = true };

            if (await userManager.FindByEmailAsync(m1User.Email) == null)
            {
                await userManager.CreateAsync(m1User, "Mentor@123");
                await userManager.AddToRoleAsync(m1User, "Mentor");
                context.Mentors.Add(new Mentor { UserId = m1User.Id, FullName = "Mr. Azeez" });
            }

            if (await userManager.FindByEmailAsync(m2User.Email) == null)
            {
                await userManager.CreateAsync(m2User, "Mentor@123");
                await userManager.AddToRoleAsync(m2User, "Mentor");
                context.Mentors.Add(new Mentor { UserId = m2User.Id, FullName = "Mr. Taofeek" });
            }

            await context.SaveChangesAsync();
        }

        // --- FETCH TRACKS FOR MODULE SEEDING ---
        var backendTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "BEC");
        var frontendTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "FEJ");
        var fullstackTrack = await context.Tracks.FirstOrDefaultAsync(t => t.Code == "FSC");

        // ==========================================
        // 5. STANDARD MODULES (Frontend, Backend, Fullstack)
        // ==========================================
        if (!context.SyllabusModules.Any(m => m.TrackId == frontendTrack.Id || m.TrackId == backendTrack.Id || m.TrackId == fullstackTrack.Id))
        {
            var modules = new List<SyllabusModule>();

            // --- A. FRONTEND JS ---
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

            // --- B. BACKEND C# ---
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

            // --- C. FULLSTACK ---
            if (fullstackTrack != null)
            {
                modules.AddRange(new[]
                {
                    new SyllabusModule { DisplayOrder = 1, ModuleCode = "C#-01", ModuleName = "Syntax and Basics", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Beginner", Topics = "Variables, Operators, Control Structures, Loops, Exception Handling" },
                    new SyllabusModule { DisplayOrder = 2, ModuleCode = "C#-02", ModuleName = "Object-Oriented Programming (OOP)", TrackId = fullstackTrack.Id, RequiredHours = 15, DifficultyLevel = "Intermediate", Topics = "Classes, Objects, Inheritance, Polymorphism, Encapsulation, Interfaces" },
                    new SyllabusModule { DisplayOrder = 3, ModuleCode = "C#-03", ModuleName = "Advanced OOP Concepts", TrackId = fullstackTrack.Id, RequiredHours = 15, DifficultyLevel = "Advanced", Topics = "Delegates, Events, Generics, Extension Methods, LINQ, Async/Await" },
                    new SyllabusModule { DisplayOrder = 4, ModuleCode = "NET-01", ModuleName = ".NET Framework & .NET Core", TrackId = fullstackTrack.Id, RequiredHours = 5, DifficultyLevel = "Beginner", Topics = ".NET Basics, Ecosystem, Framework vs Core vs .NET 5/6/7+" },
                    new SyllabusModule { DisplayOrder = 5, ModuleCode = "NET-02", ModuleName = "Assemblies and Namespaces", TrackId = fullstackTrack.Id, RequiredHours = 5, DifficultyLevel = "Beginner", Topics = "Creating Assemblies, Organizing Code" },
                    new SyllabusModule { DisplayOrder = 6, ModuleCode = "DAT-01", ModuleName = "Data Access (ADO.NET)", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Intermediate", Topics = "Connecting to DB, Executing Commands, DataReaders, DataAdapters" },
                    new SyllabusModule { DisplayOrder = 7, ModuleCode = "DAT-02", ModuleName = "Entity Framework (EF)", TrackId = fullstackTrack.Id, RequiredHours = 15, DifficultyLevel = "Intermediate", Topics = "Code-First, Database-First, Querying Data, Migrations" },
                    new SyllabusModule { DisplayOrder = 8, ModuleCode = "WEB-01", ModuleName = "ASP.NET for Web Development", TrackId = fullstackTrack.Id, RequiredHours = 20, DifficultyLevel = "Intermediate", Topics = "MVC Architecture, Controllers, Views, Models, Routing, Validation" },
                    new SyllabusModule { DisplayOrder = 9, ModuleCode = "WEB-02", ModuleName = "ASP.NET Core", TrackId = fullstackTrack.Id, RequiredHours = 20, DifficultyLevel = "Advanced", Topics = "Middleware, Dependency Injection, Razor Pages, RESTful APIs" },
                    new SyllabusModule { DisplayOrder = 10, ModuleCode = "TST-01", ModuleName = "Testing", TrackId = fullstackTrack.Id, RequiredHours = 8, DifficultyLevel = "Intermediate", Topics = "Unit Testing, NUnit, MSTest, xUnit" },
                    new SyllabusModule { DisplayOrder = 11, ModuleCode = "GIT-01", ModuleName = "Version Control", TrackId = fullstackTrack.Id, RequiredHours = 5, DifficultyLevel = "Beginner", Topics = "Git Basics, Cloning, Branching, Merging, Pull Requests, GitHub/GitLab" },
                    new SyllabusModule { DisplayOrder = 12, ModuleCode = "DEV-01", ModuleName = "Development Tools", TrackId = fullstackTrack.Id, RequiredHours = 2, DifficultyLevel = "Beginner", Topics = "Visual Studio, VS Code Features" },
                    new SyllabusModule { DisplayOrder = 13, ModuleCode = "BLD-01", ModuleName = "Build Tools", TrackId = fullstackTrack.Id, RequiredHours = 2, DifficultyLevel = "Beginner", Topics = "MSBuild, .NET CLI" },
                    new SyllabusModule { DisplayOrder = 14, ModuleCode = "PKG-01", ModuleName = "Package Managers", TrackId = fullstackTrack.Id, RequiredHours = 2, DifficultyLevel = "Beginner", Topics = "NuGet Usage" },
                    new SyllabusModule { DisplayOrder = 15, ModuleCode = "DES-01", ModuleName = "Design Patterns", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Advanced", Topics = "Creational Patterns, Singleton, Factory Method, Abstract Factory" },
                    new SyllabusModule { DisplayOrder = 16, ModuleCode = "SQL-01", ModuleName = "SQL Fundamentals", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Intermediate", Topics = "Queries, Joins, Table Creation" },
                    new SyllabusModule { DisplayOrder = 17, ModuleCode = "API-01", ModuleName = "API Integration", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Advanced", Topics = "Consuming APIs, HTTP Clients, Postman" },
                    new SyllabusModule { DisplayOrder = 18, ModuleCode = "OPS-01", ModuleName = "Docker & Containers", TrackId = fullstackTrack.Id, RequiredHours = 10, DifficultyLevel = "Advanced", Topics = "Containerization basics, Images, Dockerfile" }
                });
            }

            context.SyllabusModules.AddRange(modules);
            await context.SaveChangesAsync();
        }

        // --- SEPARATE CHECK FOR CAPSTONE (Module 19) ---
        // Ensure this is added even if other modules exist
        if (fullstackTrack != null && !context.SyllabusModules.Any(m => m.ModuleCode == "CAP-01"))
        {
            context.SyllabusModules.Add(new SyllabusModule
            {
                DisplayOrder = 19,
                ModuleCode = "CAP-01",
                ModuleName = "Capstone Mini-Project",
                TrackId = fullstackTrack.Id,
                RequiredHours = 40,
                DifficultyLevel = "Expert",
                Topics = "Build a full e-commerce app, Integrate Payment, Deploy to Cloud",
                HasProject = true,
                IsMiniProject = true
            });
            await context.SaveChangesAsync();
        }

        // ==========================================
        // 6. API MODULES (Separate Block - Fixed Variable Name)
        // ==========================================
        if (apiTrack != null && !context.SyllabusModules.Any(m => m.TrackId == apiTrack.Id))
        {
            // Renamed 'modules' to 'apiModules' to avoid CS0136 Error
            var apiModules = new List<SyllabusModule>
            {
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "C#-01", ModuleName = "C# Fundamentals", Topics = "Syntax, Variables, Methods", RequiredHours = 10, DisplayOrder = 1, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "C#-02", ModuleName = "Object Oriented Programming", Topics = "Classes, Inheritance, SOLID", RequiredHours = 15, DisplayOrder = 2, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "DB-01", ModuleName = "Database Design & SQL", Topics = "Relational DBs, SQL Queries", RequiredHours = 10, DisplayOrder = 3, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "NET-01", ModuleName = "Entity Framework Core", Topics = "ORM, DbContext, Migrations", RequiredHours = 15, DisplayOrder = 4, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "API-01", ModuleName = "Building REST APIs", Topics = "Controllers, Routing, HTTP", RequiredHours = 20, DisplayOrder = 5, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "API-02", ModuleName = "Advanced API Concepts", Topics = "JWT Auth, Middleware, Swagger", RequiredHours = 20, DisplayOrder = 6, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "TST-01", ModuleName = "Testing & Deployment", Topics = "Unit Testing, CI/CD, Azure", RequiredHours = 15, DisplayOrder = 7, IsActive = true },
                new SyllabusModule { TrackId = apiTrack.Id, ModuleCode = "PROJ-API", ModuleName = "Capstone Project: E-Commerce API", Topics = "Full Backend with Auth & Payments", RequiredHours = 40, DisplayOrder = 8, IsActive = true, HasProject = true }
            };

            context.SyllabusModules.AddRange(apiModules);
            await context.SaveChangesAsync();
        }

        // ==========================================
        // 7. SEED RESOURCES (API & Others)
        // ==========================================
        if (apiTrack != null && !context.Resources.Any(r => r.TrackId == apiTrack.Id))
        {
            var apiResources = new List<Resource>
            {
                new Resource { TrackId = apiTrack.Id, Title = "C# 101 Video Series", Description = "Official Microsoft guide.", Url = "https://dotnet.microsoft.com/en-us/learn/csharp", Type = "Video", CreatedAt = DateTime.UtcNow },
                new Resource { TrackId = apiTrack.Id, Title = "EF Core Guide", Description = "Connect C# to SQL.", Url = "https://www.entityframeworktutorial.net/efcore/entity-framework-core.aspx", Type = "Link", CreatedAt = DateTime.UtcNow },
                new Resource { TrackId = apiTrack.Id, Title = "Build a REST API", Description = "Step-by-step tutorial.", Url = "https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api", Type = "Link", CreatedAt = DateTime.UtcNow },
                new Resource { TrackId = apiTrack.Id, Title = "E-Commerce Starter Kit", Description = "Capstone Boilerplate.", Url = "https://github.com/dotnet-architecture/eShopOnWeb", Type = "GitRepo", CreatedAt = DateTime.UtcNow }
            };

            context.Resources.AddRange(apiResources);
            await context.SaveChangesAsync();
        }

        // ==========================================
        // 8. SEED MODULE RESOURCES (Old Table - ModuleResources)
        // ==========================================
        if (!context.ModuleResources.Any())
        {
            var resources = new List<ModuleResource>();

            // Helper function to find module ID by code
            int GetModId(string code) => context.SyllabusModules.FirstOrDefault(m => m.ModuleCode == code)?.Id ?? 0;

            // 1. Syntax & Basics
            int mod1 = GetModId("C#-01");
            if (mod1 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod1, Title = "C# Full Course (Playlist)", Url = "https://www.youtube.com/playlist?list=PL82C6-O4XrHfoN_Y4MwGvJz5BntiL0z0D", Type = "Video" });
                resources.Add(new ModuleResource { ModuleId = mod1, Title = "Variables & Data Types", Url = "https://www.youtube.com/watch?v=gfkTfcpWqAY", Type = "Video" });
            }

            // 3. Advanced OOP
            int mod3 = GetModId("C#-03");
            if (mod3 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod3, Title = "LINQ Tutorial (Microsoft)", Url = "https://www.youtube.com/watch?v=eyVlsSqrOyE", Type = "Video" });
            }

            // 15. Design Patterns
            int mod15 = GetModId("DES-01");
            if (mod15 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod15, Title = "Design Patterns Practice", Url = "https://www.youtube.com/playlist?list=PLDlWc9AfQBfZIkdVaOQXi1tizJeNJipEx", Type = "Video" });
            }

            // 16. SQL
            int mod16 = GetModId("SQL-01");
            if (mod16 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod16, Title = "SQL Crash Course", Url = "https://www.youtube.com/watch?v=7S_tz1z_5bA", Type = "Video" });
            }

            // 17. API
            int mod17 = GetModId("API-01");
            if (mod17 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod17, Title = "REST API Tutorial", Url = "https://www.youtube.com/watch?v=pKd0Rpw7O48", Type = "Video" });
            }

            // 18. Docker
            int mod18 = GetModId("OPS-01");
            if (mod18 > 0)
            {
                resources.Add(new ModuleResource { ModuleId = mod18, Title = "Docker for Beginners", Url = "https://www.youtube.com/watch?v=pTFZFxd4hOI", Type = "Video" });
            }

            context.ModuleResources.AddRange(resources);
            await context.SaveChangesAsync();
        }
    }
}