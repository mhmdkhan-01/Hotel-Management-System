using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hotel_Management_System.Models;

namespace Hotel_Management_System.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            // 1. Run migrations if database isn't built yet
            if (context.Database.GetPendingMigrations().Any())
            {
                await context.Database.MigrateAsync();
            }

            // 2. Seed System Settings
            if (!await context.SystemSettings.AnyAsync())
            {
                context.SystemSettings.Add(new SystemSetting
                {
                    HotelName = "Grand Bistro",
                    FixedTaxCashPercent = 5.0m,
                    FixedTaxCardPercent = 8.0m
                });
                await context.SaveChangesAsync();
            }

            // 3. Seed Roles
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            string[] roles = ["Admin", "Kitchen", "Waiter"];

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 4. Seed Default Admin User
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            string adminEmail = "admin@hotel.com";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}