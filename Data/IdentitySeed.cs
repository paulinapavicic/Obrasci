using Microsoft.AspNetCore.Identity;
using Obrasci.Models;

namespace Obrasci.Data
{
    public static class IdentitySeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                             .CreateLogger("IdentitySeed");

            const string adminRoleName = "Admin";
            const string adminEmail = "admin@example.com";
            const string adminPassword = "Admin123!"; 

           
            if (!await roleManager.RoleExistsAsync(adminRoleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRoleName));
                if (!roleResult.Succeeded)
                    logger.LogError("Failed to create Admin role: {errors}", roleResult.Errors);
            }

            
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                //Factory Method- creating users
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Package = PackageType.Gold
                };

                var userResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!userResult.Succeeded)
                {
                    logger.LogError("Failed to create Admin user: {errors}", userResult.Errors);
                    return;
                }
            }

           
            if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, adminRoleName);
                if (!addRoleResult.Succeeded)
                    logger.LogError("Failed to add Admin role to user: {errors}", addRoleResult.Errors);
            }
        }
    }
}
