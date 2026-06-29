using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Data;
using Hotel_Management_System.Models;
using Microsoft.EntityFrameworkCore.SqlServer;
using Hotel_Management_System.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// 1. Add Connection String and DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Add ASP.NET Core Identity Framework
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    // Keeping password rules basic and accessible for your staff setup
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Configure Authentication Cookie redirection options
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8); // Matches an active work shift
});

// 4. Add Controllers with Views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 5. Invoke SeedData to create roles and the root administrator profile automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 🌟 FIXED: Added .GetAwaiter().GetResult() to prevent top-level compilation errors
        SeedData.Initialize(services).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database layout.");
    }
}

// 6. Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 🌟 THE FIX: Only redirect to HTTPS if we are NOT in the Development environment
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
var app2 = app.UseAuthorization();

// 7. Route Mapping
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Customer}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/notificationHub");

app.Run();