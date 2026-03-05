using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazorClaw.Server.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Apply all migrations
            await context.Database.MigrateAsync();
            logger.LogInformation("✅ Database migrations applied");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error applying migrations");
            throw;
        }

        // Seed Roles → Admin User
        await SeedRolesAsync(roleManager, logger);
        await SeedAdminUserAsync(userManager, roleManager, context, configuration, logger);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        var roles = new[]
        {
            new { Name = "Admin", Description = "System Administrator" },
            new { Name = "User", Description = "Regular User" },
            new { Name = "Guest", Description = "Guest User (read-only)" }
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role.Name))
            {
                var applicationRole = new ApplicationRole
                {
                    Name = role.Name,
                    Description = role.Description,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await roleManager.CreateAsync(applicationRole);
                if (result.Succeeded)
                {
                    logger.LogInformation("✅ Role created: {Role}", role.Name);
                }
                else
                {
                    logger.LogError("❌ Failed to create role {Role}: {Errors}",
                        role.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger logger)
    {
        // Ensure at least one admin exists
        var adminRole = await roleManager.FindByNameAsync("Admin");
        var adminCount = adminRole != null ? context.UserRoles
            .Where(ur => ur.RoleId == adminRole.Id)
            .Count() : 0;

        if (adminCount == 0)
        {
            var adminEmail = configuration["Database:DefaultAdminEmail"] ?? "admin@localhost";
            var adminPassword = configuration["Database:DefaultAdminPassword"] ?? "Admin123!";
            
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);

                if (result.Succeeded)
                {
                    logger.LogInformation("✅ Default admin user created: {Email}", adminEmail);
                    logger.LogWarning("⚠️ SECURITY: Change the default admin password immediately!");
                }
                else
                {
                    logger.LogError("❌ Failed to create default admin: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                    return;
                }
            }

            // Assign Admin role
            if (adminRole != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("✅ Admin role assigned to: {Email}", adminEmail);
                }
            }
        }
    }
}