using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TankDesigner.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<ProyectoEntidad> Proyectos { get; set; }
        public DbSet<InvitacionUsuario> InvitacionesUsuario => Set<InvitacionUsuario>();

    }
}