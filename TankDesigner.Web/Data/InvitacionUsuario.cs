namespace TankDesigner.Web.Data
{
    public class InvitacionUsuario
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