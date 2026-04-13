using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    // Servicio encargado de gestionar invitaciones de usuarios (Admin / SuperAdmin)
    public class InvitacionesService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        // Constructor con inyección de dependencias
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

        // Crea una nueva invitación para un email con un rol determinado
        public async Task<CrearInvitacionResultadoDto> CrearInvitacionAsync(string email, string rolAsignado, string? creadaPorUserId)
        {
            // Normalización de datos
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            rolAsignado = (rolAsignado ?? string.Empty).Trim();

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(email))
                return new CrearInvitacionResultadoDto(false, "Debes indicar un correo electrónico.");

            if (rolAsignado != RolesAplicacion.Admin && rolAsignado != RolesAplicacion.SuperAdmin)
                return new CrearInvitacionResultadoDto(false, "El rol asignado no es válido.");

            // Elimina invitaciones anteriores no usadas para ese email
            var invitacionesPendientes = await _db.InvitacionesUsuario
                .Where(i => i.Email == email && !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .ToListAsync();

            if (invitacionesPendientes.Count > 0)
                _db.InvitacionesUsuario.RemoveRange(invitacionesPendientes);

            // Crea nueva invitación
            var invitacion = new InvitacionUsuario
            {
                Email = email,
                Token = Guid.NewGuid().ToString("N"), // Token único
                RolAsignado = rolAsignado,
                FechaCreacion = DateTime.UtcNow,
                FechaExpiracion = DateTime.UtcNow.AddDays(7), // Expira en 7 días
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

        // Devuelve todas las invitaciones pendientes (no usadas y no expiradas)
        public async Task<List<InvitacionUsuarioDto>> ObtenerInvitacionesPendientesAsync()
        {
            var invitaciones = await _db.InvitacionesUsuario
                .Where(i => !i.Usada && i.FechaExpiracion > DateTime.UtcNow)
                .OrderByDescending(i => i.FechaCreacion)
                .ToListAsync();

            return invitaciones.Select(Mapear).ToList();
        }

        // Obtiene la invitación pendiente más reciente por email
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

        // Obtiene una invitación válida mediante su token
        public async Task<InvitacionUsuarioDto?> ObtenerInvitacionValidaPorTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var invitacion = await _db.InvitacionesUsuario
                .FirstOrDefaultAsync(i => i.Token == token && !i.Usada && i.FechaExpiracion > DateTime.UtcNow);

            return invitacion is null ? null : Mapear(invitacion);
        }

        // Aplica automáticamente una invitación pendiente cuando el usuario inicia sesión
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

            // Asigna roles según invitación
            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            // Marca la invitación como usada
            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        // Aplica invitación mediante token (enlace de invitación)
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

            // Verifica que el email del usuario coincide con el de la invitación
            if (!string.Equals(invitacion.Email, emailUsuario, StringComparison.OrdinalIgnoreCase))
                return new AplicarInvitacionResultadoDto(true, false, null);

            await AplicarRolSegunInvitacionAsync(usuario, invitacion);

            invitacion.Usada = true;
            await _db.SaveChangesAsync();

            return new AplicarInvitacionResultadoDto(true, true, invitacion.RolAsignado);
        }

        // Asigna los roles al usuario según la invitación
        private async Task AplicarRolSegunInvitacionAsync(ApplicationUser usuario, InvitacionUsuario invitacion)
        {
            var rolesActuales = await _userManager.GetRolesAsync(usuario);

            // Añade el rol principal
            if (!rolesActuales.Contains(invitacion.RolAsignado))
                await _userManager.AddToRoleAsync(usuario, invitacion.RolAsignado);

            // Si es SuperAdmin, también añade Admin automáticamente
            if (invitacion.RolAsignado == RolesAplicacion.SuperAdmin && !rolesActuales.Contains(RolesAplicacion.Admin))
                await _userManager.AddToRoleAsync(usuario, RolesAplicacion.Admin);

            // Quita el rol Usuario si lo tiene
            if (await _userManager.IsInRoleAsync(usuario, RolesAplicacion.Usuario))
                await _userManager.RemoveFromRoleAsync(usuario, RolesAplicacion.Usuario);
        }

        // Construye el enlace de invitación que se enviará al usuario
        private string ConstruirEnlaceInvitacion(string token, string email)
        {
            var baseUrl = _configuration["App:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = "https://localhost:5001";

            baseUrl = baseUrl.Trim().TrimEnd('/');

            return $"{baseUrl}/invitacion/{Uri.EscapeDataString(token)}?email={Uri.EscapeDataString(email)}";
        }

        // Genera el HTML del correo de invitación
        private string ConstruirHtmlInvitacion(string email, string rolAsignado, string enlace, DateTime fechaExpiracionUtc)
        {
            var fechaTexto = fechaExpiracionUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            return $@"
            <!-- HTML del correo -->
            ...
            ";
        }

        // Convierte entidad a DTO
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

    // Resultado al crear una invitación
    public record CrearInvitacionResultadoDto(bool Ok, string Mensaje, InvitacionUsuarioDto? Invitacion = null)
    {
        public string Token => Invitacion?.Token ?? string.Empty;
        public string Email => Invitacion?.Email ?? string.Empty;
        public string RolAsignado => Invitacion?.RolAsignado ?? string.Empty;
        public DateTime? FechaExpiracion => Invitacion?.FechaExpiracion;
    }

    // Resultado al aplicar una invitación
    public record AplicarInvitacionResultadoDto(bool Encontrada, bool Aplicada, string? RolAplicado);

    // DTO de invitación
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