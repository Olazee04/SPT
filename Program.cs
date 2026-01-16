using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Services
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // 🔴 REQUIRED: Enables Identity Pages (Login/Logout)

// 2. Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    // Optional: Relax password requirements for development if needed
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 4. Configure Cookie (Optional but helpful for fixing login loops)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

var app = builder.Build();

// 5. Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // 🔴 REQUIRED: Checks "Who are you?"
app.UseAuthorization();  // 🔴 REQUIRED: Checks "Allowed to be here?"

// 6. Map Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages(); // 🔴 REQUIRED: This makes the Login/Logout pages work!

// 7. Seed Database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Ensure database is created (optional safety check)
    // await context.Database.MigrateAsync(); 
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();