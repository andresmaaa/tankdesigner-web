using Microsoft.AspNetCore.Identity;

namespace TankDesigner.Web.Data
{
    // Clase estática encargada de crear los roles básicos
    // y de asegurar que el usuario definido en configuración
    // tenga permisos de SuperAdmin y Admin.
    public static class IdentitySeedData
    {
        // Método principal que se ejecuta al arrancar la aplicación.
        // Se encarga de:
        // 1. Crear los roles si no existen.
        // 2. Buscar el email configurado como SuperAdmin.
        // 3. Asignarle los roles necesarios.
        public static async Task InicializarAsync(IServiceProvider services, IConfiguration configuration)
        {
            // Crea un scope para poder resolver servicios con ciclo de vida scoped.
            using var scope = services.CreateScope();

            // Obtiene los servicios de Identity necesarios.
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Crea el rol Usuario si todavía no existe en la base de datos.
            if (!await roleManager.RoleExistsAsync(RolesAplicacion.Usuario))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.Usuario));

            // Crea el rol Admin si todavía no existe.
            if (!await roleManager.RoleExistsAsync(RolesAplicacion.Admin))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.Admin));

            // Crea el rol SuperAdmin si todavía no existe.
            if (!await roleManager.RoleExistsAsync(RolesAplicacion.SuperAdmin))
                await roleManager.CreateAsync(new IdentityRole(RolesAplicacion.SuperAdmin));

            // Lee desde configuración el email que debe actuar como SuperAdmin inicial.
            var emailSuperAdmin = configuration["AdminSeed:Email"];

            // Si no hay email configurado, termina aquí.
            if (string.IsNullOrWhiteSpace(emailSuperAdmin))
                return;

            // Busca en la base de datos el usuario con ese email.
            var usuario = await userManager.FindByEmailAsync(emailSuperAdmin);

            // Si el usuario no existe todavía, termina aquí.
            // Esto evita errores si la app arranca antes de que ese usuario se registre.
            if (usuario == null)
                return;

            // Si no tiene el rol SuperAdmin, se lo añade.
            if (!await userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
                await userManager.AddToRoleAsync(usuario, RolesAplicacion.SuperAdmin);

            // Si no tiene el rol Admin, también se lo añade.
            // Esto hace que el SuperAdmin herede capacidades de Admin.
            if (!await userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            // Si el usuario tenía el rol Usuario normal, se le quita.
            // Así evitamos que el SuperAdmin quede mezclado con el rol básico.
            if (await userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Usuario);
        }
    }
}