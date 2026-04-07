using System.ComponentModel.DataAnnotations;

namespace TankDesigner.Web.Data
{
    public class ProyectoEntidad
    {
        public int Id { get; set; }

        [Required]
        public string UsuarioId { get; set; } = "";

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = "";

        [MaxLength(200)]
        public string Cliente { get; set; } = "";

        [MaxLength(100)]
        public string Normativa { get; set; } = "";

        [MaxLength(100)]
        public string Fabricante { get; set; } = "";

        public string ProyectoJson { get; set; } = "";
        public string TanqueJson { get; set; } = "";
        public string CargasJson { get; set; } = "";
        public string InstalacionJson { get; set; } = "";
        public string ResultadoJson { get; set; } = "";

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
    }
}