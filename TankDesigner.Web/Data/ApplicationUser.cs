using Microsoft.AspNetCore.Identity;

namespace TankDesigner.Web.Data
{
    public class ApplicationUser : IdentityUser
    {
        // 👤 Perfil
        public string NombreCompleto { get; set; } = string.Empty;
        public string? TelefonoContacto { get; set; }
        public string? Cargo { get; set; }

        // 🏢 Empresa
        public string? EmpresaNombre { get; set; }
        public string? EmpresaDireccion { get; set; }
        public string? EmpresaCiudad { get; set; }
        public string? EmpresaProvincia { get; set; }
        public string? EmpresaCodigoPostal { get; set; }
        public string? EmpresaPais { get; set; }
        public string? EmpresaWeb { get; set; }
        public string? EmpresaIdentificacionFiscal { get; set; }
    }
}