using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    public class InvitacionesService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public InvitacionesService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<CrearInvitacionResultadoDto> CrearInvitacionAsync(string email, string rolAsignado, string? creadaPorUserId)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            rolAsignado = (rolAsignado ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email))
                return new CrearInvitacionResultadoDto(false, "Debes indicar un correo electrónico.");

            if (rolAsignado != RolesAplicacion.Admin && rolAsignado != RolesAplicacion.SuperAdmin)
                return new CrearInvitacionResultadoDto(false, "El rol asignado no es válido.");

            var invitacionesPendientes = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .ToListAsync();

            if (invitacionesPendientes.Count > 0)
                _db.InvitacionesUsuario.RemoveRange(invitacionesPendientes);

            var invitacion = new InvitacionUsuario
            {
                Email = email,
                Token = Guid.NewGuid().ToString("N"),
                RolAsignado = rolAsignado,
                FechaCreacion = DateTime.UtcNow,
                FechaExpiracion = DateTime.UtcNow.AddDays(7),
                Usada = false,
                CreadaPorUserId = creadaPorUserId
            };

            _db.InvitacionesUsuario.Add(invitacion);
            await _db.SaveChangesAsync();

            return new CrearInvitacionResultadoDto(true, "Invitación generada correctamente.", Mapear(invitacion));
        }

        public async Task<List<InvitacionUsuarioDto>> ObtenerInvitacionesPendientesAsync()
        {
            var invitaciones = await _db.InvitacionesUsuario
                .Where(i => !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .OrderByDescending(i => i.FechaCreacion)
                .ToListAsync();

            return invitaciones.Select(Mapear).ToList();
        }


        public async Task<InvitacionUsuarioDto?> ObtenerInvitacionPendientePorEmailAsync(string? email)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return null;

            var invitacion = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            return invitacion is null ? null : Mapear(invitacion);
        }

        public async Task<InvitacionUsuarioDto?> ObtenerInvitacionValidaPorTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var invitacion = await _db.InvitacionesUsuario
                .FirstOrDefaultAsync(i => i.Token == token && !i.Usada && i.FechaExpiracion > DateTime.UtcNow);

            return invitacion is null ? null : Mapear(invitacion);
        }

        public async Task<AplicarInvitacionResultadoDto> AplicarInvitacionPendienteAsync(ApplicationUser usuario)
        {
            var email = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email))
                return new AplicarInvitacionResultadoDto(false, false, null);

            var invitacion = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            if (invitacion is null)
                return new AplicarInvitacionResultadoDto(false, false, null);

            var rolesActuales = await _userManager.GetRolesAsync(usuario);

            if (!rolesActuales.Contains(invitacion.RolAsignado))
                await _userManager.AddToRoleAsync(usuario, invitacion.RolAsignado);

            if (invitacion.RolAsignado == RolesAplicacion.SuperAdmin && !rolesActuales.Contains(RolesAplicacion.Admin))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Usuario);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        private static InvitacionUsuarioDto Mapear(InvitacionUsuario invitacion)
        {
            return new InvitacionUsuarioDto
            {
                Id = invitacion.Id,
                Email = invitacion.Email,
                Token = invitacion.Token,
                RolAsignado = invitacion.RolAsignado,
                FechaCreacion = invitacion.FechaCreacion,
                FechaExpiracion = invitacion.FechaExpiracion,
                Usada = invitacion.Usada,
                CreadaPorUserId = invitacion.CreadaPorUserId
            };
        }
    }

    public record CrearInvitacionResultadoDto(bool Ok, string Mensaje, InvitacionUsuarioDto? Invitacion = null);
    public record AplicarInvitacionResultadoDto(bool Encontrada, bool Aplicada, string? RolAplicado);

    public class InvitacionUsuarioDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string RolAsignado { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public bool Usada { get; set; }
        public string? CreadaPorUserId { get; set; }
    }
}
