using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    // Servicio encargado de toda la lógica de administración:
    // - Gestión de usuarios (roles)
    // - Consulta de proyectos globales
    // - Eliminación de proyectos
    public class AdminService
    {
        // Identity para gestionar usuarios y roles
        private readonly UserManager<ApplicationUser> _userManager;

        // Factory para crear DbContext
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _userManager = userManager;
            _dbContextFactory = dbContextFactory;
        }

        // Obtiene todos los usuarios con sus roles
        public async Task<List<UsuarioAdminDto>> ObtenerUsuariosAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Se obtienen los datos básicos de usuarios
            var usuarios = await context.Users
                .OrderBy(u => u.Email)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.NombreCompleto
                })
                .ToListAsync();

            var userIds = usuarios.Select(u => u.Id).ToList();

            // Se obtienen los roles de cada usuario
            var rolesPorUsuario = await context.UserRoles
                .Join(
                    context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name ?? "" })
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync();

            // Se construye el DTO final combinando usuarios + roles
            return usuarios.Select(usuario =>
            {
                var roles = rolesPorUsuario
                    .Where(r => r.UserId == usuario.Id)
                    .Select(r => r.RoleName)
                    .ToList();

                return new UsuarioAdminDto
                {
                    Id = usuario.Id,
                    Email = usuario.Email ?? "",
                    NombreCompleto = usuario.NombreCompleto ?? "",

                    // Flags de roles
                    EsAdmin = roles.Contains(RolesAplicacion.Admin),
                    EsUsuario = roles.Contains(RolesAplicacion.Usuario),
                    EsSuperAdmin = roles.Contains(RolesAplicacion.SuperAdmin)
                };
            }).ToList();
        }

        // Convierte un usuario en Admin (solo si el actual es SuperAdmin)
        public async Task<bool> HacerAdminAsync(string userId, ClaimsPrincipal usuarioActual)
        {
            // Seguridad: solo SuperAdmin puede hacer Admin
            if (!usuarioActual.IsInRole(RolesAplicacion.SuperAdmin))
                return false;

            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
                return false;

            // No se puede modificar un SuperAdmin
            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
                return false;

            // Asegura que tenga rol Usuario
            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Usuario);

            // Añade rol Admin
            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            return true;
        }

        // Quita el rol Admin (solo si el actual es SuperAdmin)
        public async Task<bool> QuitarAdminAsync(string userId, ClaimsPrincipal usuarioActual)
        {
            // Seguridad
            if (!usuarioActual.IsInRole(RolesAplicacion.SuperAdmin))
                return false;

            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
                return false;

            // No se puede modificar un SuperAdmin
            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
                return false;

            // Quita rol Admin
            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await _userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Admin);

            // Asegura que tenga rol Usuario
            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Usuario);

            return true;
        }

        // Devuelve todos los proyectos del sistema (para panel admin)
        public async Task<List<ProyectoAdminDto>> ObtenerTodosLosProyectosAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Se obtienen todos los proyectos
            var proyectos = await context.Proyectos
                .OrderByDescending(p => p.FechaModificacion)
                .ToListAsync();

            // Se obtiene un diccionario de usuarios para mapear email
            var usuarios = await context.Users
                .Select(u => new { u.Id, u.Email })
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? "—");

            // Se construye el DTO final
            return proyectos.Select(p => new ProyectoAdminDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Cliente = p.Cliente,
                Normativa = p.Normativa,
                Fabricante = p.Fabricante,
                UsuarioId = p.UsuarioId,

                // Se obtiene email del usuario
                EmailUsuario = usuarios.TryGetValue(p.UsuarioId, out var email) ? email : "—",

                FechaCreacion = p.FechaCreacion,
                FechaModificacion = p.FechaModificacion
            }).ToList();
        }

        // Elimina un proyecto (uso admin)
        public async Task<bool> EliminarProyectoAsync(int proyectoId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var proyecto = await context.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId);
            if (proyecto == null)
                return false;

            context.Proyectos.Remove(proyecto);
            await context.SaveChangesAsync();

            return true;
        }
    }

    // DTO para mostrar usuarios en panel admin
    public class UsuarioAdminDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string NombreCompleto { get; set; } = "";

        public bool EsAdmin { get; set; }
        public bool EsUsuario { get; set; }
        public bool EsSuperAdmin { get; set; }
    }

    // DTO para mostrar proyectos en panel admin
    public class ProyectoAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Normativa { get; set; } = "";
        public string Fabricante { get; set; } = "";
        public string UsuarioId { get; set; } = "";
        public string EmailUsuario { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
    }
}