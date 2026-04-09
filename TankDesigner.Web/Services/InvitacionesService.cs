using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    public class InvitacionesService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public InvitacionesService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IConfiguration configuration)
        {
            _db = db;
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
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

            var dto = Mapear(invitacion);

            return new CrearInvitacionResultadoDto(
                true,
                "Invitación generada correctamente.",
                dto);
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

            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        public async Task<AplicarInvitacionResultadoDto> AplicarInvitacionPorTokenAsync(ApplicationUser usuario, string? token)
        {
            token = (token ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(token))
                return new AplicarInvitacionResultadoDto(false, false, null);

            var emailUsuario = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(emailUsuario))
                return new AplicarInvitacionResultadoDto(false, false, null);

            var invitacion = await _db.InvitacionesUsuario
                .FirstOrDefaultAsync(i => i.Token == token && !i.Usada && i.FechaExpiracion > DateTime.UtcNow);

            if (invitacion is null)
                return new AplicarInvitacionResultadoDto(false, false, null);

            if (!string.Equals(invitacion.Email, emailUsuario, StringComparison.OrdinalIgnoreCase))
                return new AplicarInvitacionResultadoDto(true, false, null);

            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        private async Task AplicarRolSegunInvitacionAsync(ApplicationUser usuario, InvitacionUsuario invitacion)
        {
            var rolesActuales = await _userManager.GetRolesAsync(usuario);

            if (!rolesActuales.Contains(invitacion.RolAsignado))
                await _userManager.AddToRoleAsync(usuario, invitacion.RolAsignado);

            if (invitacion.RolAsignado == RolesAplicacion.SuperAdmin && !rolesActuales.Contains(RolesAplicacion.Admin))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Usuario);
        }

        private string ConstruirEnlaceInvitacion(string token, string email)
        {
            var baseUrl = _configuration["App:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = "https://localhost:5001";

            baseUrl = baseUrl.Trim().TrimEnd('/');

            return $"{baseUrl}/invitacion/{Uri.EscapeDataString(token)}?email={Uri.EscapeDataString(email)}";
        }

        private string ConstruirHtmlInvitacion(string email, string rolAsignado, string enlace, DateTime fechaExpiracionUtc)
        {
            var fechaTexto = fechaExpiracionUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8' />
    <title>Invitación a Tank Structural Designer</title>
</head>
<body style='margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;'>
    <div style='max-width:640px;margin:40px auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:16px;overflow:hidden;'>
        <div style='background:#173b7a;padding:28px 32px;color:#ffffff;'>
            <h1 style='margin:0;font-size:24px;'>Tank Structural Designer</h1>
            <p style='margin:8px 0 0 0;font-size:14px;opacity:0.95;'>Invitación de acceso a la plataforma</p>
        </div>

        <div style='padding:32px; color:#1f2937;'>
            <p style='margin-top:0;'>Hola,</p>

            <p>Se ha generado una invitación para el correo <strong>{email}</strong>.</p>

            <p>Rol asignado: <strong>{rolAsignado}</strong></p>

            <p>La invitación estará disponible hasta el <strong>{fechaTexto}</strong>.</p>

            <div style='margin:30px 0;'>
                <a href='{enlace}' style='display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:14px 22px;border-radius:10px;font-weight:bold;'>
                    Aceptar invitación
                </a>
            </div>

            <p style='font-size:14px;color:#4b5563;'>
                Si el botón no funciona, copia y pega este enlace en tu navegador:
            </p>

            <p style='font-size:13px;word-break:break-all;color:#2563eb;'>
                {enlace}
            </p>

            <hr style='border:none;border-top:1px solid #e5e7eb;margin:28px 0;' />

            <p style='font-size:12px;color:#6b7280;margin-bottom:0;'>
                Este correo ha sido generado automáticamente por Tank Structural Designer.
            </p>
        </div>
    </div>
</body>
</html>";
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

    public record CrearInvitacionResultadoDto(bool Ok, string Mensaje, InvitacionUsuarioDto? Invitacion = null)
    {
        public string Token => Invitacion?.Token ?? string.Empty;
        public string Email => Invitacion?.Email ?? string.Empty;
        public string RolAsignado => Invitacion?.RolAsignado ?? string.Empty;
        public DateTime? FechaExpiracion => Invitacion?.FechaExpiracion;
    }

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