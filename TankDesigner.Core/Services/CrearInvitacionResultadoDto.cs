namespace TankDesigner.Web.Services
{
    public class CrearInvitacionResultadoDto
    {
        public string Email { get; set; } = string.Empty;
        public string RolAsignado { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime FechaExpiracion { get; set; }
    }
}