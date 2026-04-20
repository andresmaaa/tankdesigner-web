using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    public class AdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ILogger<AdminService> _logger;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            ILogger<AdminService> logger)
        {
            _userManager = userManager;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<List<UsuarioAdminDto>> ObtenerUsuariosAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

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

            var rolesPorUsuario = await context.UserRoles
                .Join(
                    context.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => new { ur.UserId, RoleName = r.Name ?? "" })
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync();

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
                    EsAdmin = roles.Contains(RolesAplicacion.Admin),
                    EsUsuario = roles.Contains(RolesAplicacion.Usuario),
                    EsSuperAdmin = roles.Contains(RolesAplicacion.SuperAdmin)
                };
            }).ToList();
        }

        public async Task<bool> HacerAdminAsync(string userId, ClaimsPrincipal usuarioActual)
        {
            var actorId = _userManager.GetUserId(usuarioActual);

            if (!usuarioActual.IsInRole(RolesAplicacion.SuperAdmin))
            {
                _logger.LogWarning("Intento no autorizado de hacer admin. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
            {
                _logger.LogWarning("No se encontró el usuario objetivo al hacer admin. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
            {
                _logger.LogWarning("Intento de modificar un SuperAdmin con HacerAdminAsync. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Usuario);

            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            _logger.LogInformation("Rol Admin asignado. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}. TargetEmail: {TargetEmail}",
                actorId, usuario.Id, usuario.Email);

            return true;
        }

        public async Task<bool> QuitarAdminAsync(string userId, ClaimsPrincipal usuarioActual)
        {
            var actorId = _userManager.GetUserId(usuarioActual);

            if (!usuarioActual.IsInRole(RolesAplicacion.SuperAdmin))
            {
                _logger.LogWarning("Intento no autorizado de quitar admin. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
            {
                _logger.LogWarning("No se encontró el usuario objetivo al quitar admin. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.SuperAdmin))
            {
                _logger.LogWarning("Intento de modificar un SuperAdmin con QuitarAdminAsync. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}",
                    actorId, userId);
                return false;
            }

            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Admin))
                await _userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Admin);

            if (!await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Usuario);

            _logger.LogInformation("Rol Admin retirado. ActorUserId: {ActorUserId}. TargetUserId: {TargetUserId}. TargetEmail: {TargetEmail}",
                actorId, usuario.Id, usuario.Email);

            return true;
        }

        public async Task<List<ProyectoAdminDto>> ObtenerTodosLosProyectosAsync()
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var proyectos = await context.Proyectos
                .OrderByDescending(p => p.FechaModificacion)
                .ToListAsync();

            var usuarios = await context.Users
                .Select(u => new { u.Id, u.Email })
                .ToDictionaryAsync(u => u.Id, u => u.Email ?? "—");

            return proyectos.Select(p => new ProyectoAdminDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Cliente = p.Cliente,
                Normativa = p.Normativa,
                Fabricante = p.Fabricante,
                UsuarioId = p.UsuarioId,
                EmailUsuario = usuarios.TryGetValue(p.UsuarioId, out var email) ? email : "—",
                FechaCreacion = p.FechaCreacion,
                FechaModificacion = p.FechaModificacion
            }).ToList();
        }

        public async Task<bool> EliminarProyectoAsync(int proyectoId, ClaimsPrincipal usuarioActual)
        {
            var actorId = _userManager.GetUserId(usuarioActual);

            if (!usuarioActual.IsInRole(RolesAplicacion.Admin) &&
                !usuarioActual.IsInRole(RolesAplicacion.SuperAdmin))
            {
                _logger.LogWarning("Intento no autorizado de eliminar proyecto. ActorUserId: {ActorUserId}. ProyectoId: {ProyectoId}",
                    actorId, proyectoId);
                return false;
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var proyecto = await context.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId);
            if (proyecto == null)
            {
                _logger.LogWarning("Proyecto no encontrado al eliminar. ActorUserId: {ActorUserId}. ProyectoId: {ProyectoId}",
                    actorId, proyectoId);
                return false;
            }

            context.Proyectos.Remove(proyecto);
            await context.SaveChangesAsync();

            _logger.LogInformation("Proyecto eliminado. ActorUserId: {ActorUserId}. ProyectoId: {ProyectoId}. NombreProyecto: {NombreProyecto}. UsuarioPropietarioId: {UsuarioPropietarioId}",
                actorId, proyecto.Id, proyecto.Nombre, proyecto.UsuarioId);

            return true;
        }
    }

    public class UsuarioAdminDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string NombreCompleto { get; set; } = "";

        public bool EsAdmin { get; set; }
        public bool EsUsuario { get; set; }
        public bool EsSuperAdmin { get; set; }
    }

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