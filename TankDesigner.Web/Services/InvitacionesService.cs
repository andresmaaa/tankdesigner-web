using System.Security.Cryptography;
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
        private readonly ILogger<InvitacionesService> _logger;

        public InvitacionesService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IConfiguration configuration,
            ILogger<InvitacionesService> logger)
        {
            _db = db;
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CrearInvitacionResultadoDto> CrearInvitacionAsync(string email, string rolAsignado, string? creadaPorUserId)
        {
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            rolAsignado = (rolAsignado ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Intento de crear invitación sin email. CreadaPorUserId: {CreadaPorUserId}", creadaPorUserId);
                return new CrearInvitacionResultadoDto(false, "Debes indicar un correo electrónico.");
            }

            if (rolAsignado != RolesAplicacion.Admin && rolAsignado != RolesAplicacion.SuperAdmin)
            {
                _logger.LogWarning("Intento de crear invitación con rol inválido. Email: {Email}. Rol: {Rol}. CreadaPorUserId: {CreadaPorUserId}",
                    email, rolAsignado, creadaPorUserId);
                return new CrearInvitacionResultadoDto(false, "El rol asignado no es válido.");
            }

            var invitacionesExpiradas = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion <= DateTime.UtcNow)
                .ToListAsync();

            if (invitacionesExpiradas.Count > 0)
                _db.InvitacionesUsuario.RemoveRange(invitacionesExpiradas);

            var invitacionesPendientes = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .ToListAsync();

            if (invitacionesPendientes.Count > 0)
            {
                _logger.LogInformation("Se eliminan invitaciones pendientes anteriores para {Email}. Cantidad: {Cantidad}. CreadaPorUserId: {CreadaPorUserId}",
                    email, invitacionesPendientes.Count, creadaPorUserId);

                _db.InvitacionesUsuario.RemoveRange(invitacionesPendientes);
            }

            var token = GenerarTokenSeguro();

            var invitacion = new InvitacionUsuario
            {
                Email = email,
                Token = token,
                RolAsignado = rolAsignado,
                FechaCreacion = DateTime.UtcNow,
                FechaExpiracion = DateTime.UtcNow.AddHours(48),
                Usada = false,
                CreadaPorUserId = creadaPorUserId
            };

            _db.InvitacionesUsuario.Add(invitacion);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Invitación creada. Id: {InvitacionId}. Email: {Email}. Rol: {Rol}. Expira: {Expira}. CreadaPorUserId: {CreadaPorUserId}",
                invitacion.Id, invitacion.Email, invitacion.RolAsignado, invitacion.FechaExpiracion, creadaPorUserId);

            var enviarEmailAutomatico = _configuration.GetValue<bool>("Invitaciones:EnviarEmailAutomatico");
            if (enviarEmailAutomatico)
            {
                var enlace = ConstruirEnlaceInvitacion(invitacion.Token, invitacion.Email);
                var html = ConstruirHtmlInvitacion(
                    invitacion.Email,
                    invitacion.RolAsignado,
                    enlace,
                    invitacion.FechaExpiracion);

                try
                {
                    await _emailService.EnviarEmailAsync(
                        invitacion.Email,
                        "Invitación de acceso a TankDesigner",
                        html);

                    _logger.LogInformation("Email de invitación enviado a {Email}. InvitacionId: {InvitacionId}",
                        invitacion.Email, invitacion.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando email de invitación a {Email}. InvitacionId: {InvitacionId}",
                        invitacion.Email, invitacion.Id);
                }
            }

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
            token = (token ?? string.Empty).Trim();

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
            {
                _logger.LogWarning("No se puede aplicar invitación pendiente porque el usuario no tiene email. UserId: {UserId}", usuario.Id);
                return new AplicarInvitacionResultadoDto(false, false, null);
            }

            var invitacion = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            if (invitacion is null)
                return new AplicarInvitacionResultadoDto(false, false, null);

            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Invitación pendiente aplicada. UserId: {UserId}. Email: {Email}. RolAplicado: {Rol}. InvitacionId: {InvitacionId}",
                usuario.Id, email, invitacion.RolAsignado, invitacion.Id);

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        public async Task<AplicarInvitacionResultadoDto> AplicarInvitacionPorTokenAsync(ApplicationUser usuario, string? token)
        {
            token = (token ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Intento de aplicar invitación con token vacío. UserId: {UserId}", usuario.Id);
                return new AplicarInvitacionResultadoDto(false, false, null);
            }

            var emailUsuario = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(emailUsuario))
            {
                _logger.LogWarning("Intento de aplicar invitación por token con usuario sin email. UserId: {UserId}", usuario.Id);
                return new AplicarInvitacionResultadoDto(false, false, null);
            }

            var invitacion = await _db.InvitacionesUsuario
                .FirstOrDefaultAsync(i => i.Token == token && !i.Usada && i.FechaExpiracion > DateTime.UtcNow);

            if (invitacion is null)
            {
                _logger.LogWarning("Invitación por token no encontrada o inválida. UserId: {UserId}. Email: {Email}",
                    usuario.Id, emailUsuario);
                return new AplicarInvitacionResultadoDto(false, false, null);
            }

            if (!string.Equals(invitacion.Email, emailUsuario, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("El email del usuario no coincide con el de la invitación. UserId: {UserId}. EmailUsuario: {EmailUsuario}. EmailInvitacion: {EmailInvitacion}. InvitacionId: {InvitacionId}",
                    usuario.Id, emailUsuario, invitacion.Email, invitacion.Id);

                return new AplicarInvitacionResultadoDto(true, false, null);
            }

            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Invitación por token aplicada. UserId: {UserId}. Email: {Email}. RolAplicado: {Rol}. InvitacionId: {InvitacionId}",
                usuario.Id, emailUsuario, invitacion.RolAsignado, invitacion.Id);

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        private async Task AplicarRolSegunInvitacionAsync(ApplicationUser usuario, InvitacionUsuario invitacion)
        {
            var rolesActuales = await _userManager.GetRolesAsync(usuario);

            if (!rolesActuales.Contains(invitacion.RolAsignado))
                await _userManager.AddToRoleAsync(usuario, invitacion.RolAsignado);

            if (invitacion.RolAsignado == RolesAplicacion.SuperAdmin &&
                !rolesActuales.Contains(RolesAplicacion.Admin))
            {
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);
            }

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
            <div style='font-family: Arial, sans-serif; color: #1f2937; line-height: 1.6;'>
                <h2>Invitación de acceso a TankDesigner</h2>
                <p>Se ha generado una invitación para el correo <strong>{email}</strong>.</p>
                <p>Rol asignado: <strong>{rolAsignado}</strong></p>
                <p>La invitación estará disponible hasta: <strong>{fechaTexto}</strong></p>
                <p>
                    <a href='{enlace}' style='display:inline-block;padding:10px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;'>
                        Aceptar invitación
                    </a>
                </p>
                <p>Si no esperabas este correo, puedes ignorarlo.</p>
            </div>";
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

        private static string GenerarTokenSeguro()
        {
            var tokenBytes = new byte[32];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(tokenBytes);

            return Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
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