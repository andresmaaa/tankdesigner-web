using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TankDesigner.Core.Models;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    public class ProyectoPersistenciaService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public ProyectoPersistenciaService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<int> GuardarAsync(
            string usuarioId,
            int? proyectoId,
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas,
            InstalacionModel instalacion,
            ResultadoCalculoModel resultado)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            ProyectoEntidad entidad;

            if (proyectoId.HasValue)
            {
                entidad = await context.Proyectos
                    .FirstAsync(p => p.Id == proyectoId.Value && p.UsuarioId == usuarioId);

                entidad.Nombre = string.IsNullOrWhiteSpace(proyecto.NombreProyecto)
                    ? "Proyecto sin nombre"
                    : proyecto.NombreProyecto.Trim();

                entidad.Cliente = proyecto.ClienteReferencia ?? "";
                entidad.Normativa = proyecto.Normativa ?? "";
                entidad.Fabricante = proyecto.Fabricante ?? "";
                entidad.ProyectoJson = JsonConvert.SerializeObject(proyecto);
                entidad.TanqueJson = JsonConvert.SerializeObject(tanque);
                entidad.CargasJson = JsonConvert.SerializeObject(cargas);
                entidad.InstalacionJson = JsonConvert.SerializeObject(instalacion);
                entidad.ResultadoJson = JsonConvert.SerializeObject(resultado);
                entidad.FechaModificacion = DateTime.UtcNow;
            }
            else
            {
                entidad = new ProyectoEntidad
                {
                    UsuarioId = usuarioId,
                    Nombre = string.IsNullOrWhiteSpace(proyecto.NombreProyecto)
                        ? "Proyecto sin nombre"
                        : proyecto.NombreProyecto.Trim(),
                    Cliente = proyecto.ClienteReferencia ?? "",
                    Normativa = proyecto.Normativa ?? "",
                    Fabricante = proyecto.Fabricante ?? "",
                    ProyectoJson = JsonConvert.SerializeObject(proyecto),
                    TanqueJson = JsonConvert.SerializeObject(tanque),
                    CargasJson = JsonConvert.SerializeObject(cargas),
                    InstalacionJson = JsonConvert.SerializeObject(instalacion),
                    ResultadoJson = JsonConvert.SerializeObject(resultado),
                    FechaCreacion = DateTime.UtcNow,
                    FechaModificacion = DateTime.UtcNow
                };

                context.Proyectos.Add(entidad);
            }

            await context.SaveChangesAsync();
            return entidad.Id;
        }

        public async Task<List<ProyectoEntidad>> ObtenerListaAsync(string usuarioId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Proyectos
                .Where(p => p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.FechaModificacion)
                .ToListAsync();
        }

        public async Task<bool> CargarAsync(string usuarioId, int proyectoId, ProyectoState estado)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var entidad = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            if (entidad == null)
                return false;

            estado.ProyectoIdActual = entidad.Id;
            estado.Proyecto = JsonConvert.DeserializeObject<ProyectoGeneralModel>(entidad.ProyectoJson) ?? new();
            estado.Tanque = JsonConvert.DeserializeObject<TankModel>(entidad.TanqueJson) ?? new();
            estado.Cargas = JsonConvert.DeserializeObject<CargasModel>(entidad.CargasJson) ?? new();
            estado.Instalacion = JsonConvert.DeserializeObject<InstalacionModel>(entidad.InstalacionJson) ?? new();
            estado.Resultado = JsonConvert.DeserializeObject<ResultadoCalculoModel>(entidad.ResultadoJson) ?? new();

            return true;
        }

        public async Task<int?> DuplicarAsync(string usuarioId, int proyectoId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var entidadOriginal = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            if (entidadOriginal == null)
                return null;

            var copia = new ProyectoEntidad
            {
                UsuarioId = usuarioId,
                Nombre = entidadOriginal.Nombre + " - copia",
                Cliente = entidadOriginal.Cliente,
                Normativa = entidadOriginal.Normativa,
                Fabricante = entidadOriginal.Fabricante,
                ProyectoJson = entidadOriginal.ProyectoJson,
                TanqueJson = entidadOriginal.TanqueJson,
                CargasJson = entidadOriginal.CargasJson,
                InstalacionJson = entidadOriginal.InstalacionJson,
                ResultadoJson = entidadOriginal.ResultadoJson,
                FechaCreacion = DateTime.UtcNow,
                FechaModificacion = DateTime.UtcNow
            };

            context.Proyectos.Add(copia);
            await context.SaveChangesAsync();

            return copia.Id;
        }
        public async Task<bool> EliminarAsync(string usuarioId, int proyectoId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            var entidad = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            if (entidad == null)
                return false;

            context.Proyectos.Remove(entidad);
            await context.SaveChangesAsync();
            return true;
        }
    }
}