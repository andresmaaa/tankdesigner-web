using Microsoft.AspNetCore.Identity;

namespace TankDesigner.Web.Data
{
    public static class IdentitySeedData
    {
        public static async Task InicializarAsync(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (!await roleManager.RoleExistsAsync(RolesAplicacion.Usuario))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.Usuario));

            if (!await roleManager.RoleExistsAsync(RolesAplicacion.Admin))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.Admin));

            if (!await roleManager.RoleExistsAsync(RolesAplicacion.SuperAdmin))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.SuperAdmin));

            var emailSuperAdmin = configuration["AdminSeed:Email"];

            if (string.IsNullOrWhiteSpace(emailSuperAdmin))
                return;

            var usuario = await userManager.FindByEmailAsync(emailSuperAdmin);
            if (usuario == null)
                return;

            if (!await userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
                await userManager.AddToRoleAsync(usuario, RolesAplicacion.SuperAdmin);

            if (!await userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            if (await userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Usuario);
        }
    }
}