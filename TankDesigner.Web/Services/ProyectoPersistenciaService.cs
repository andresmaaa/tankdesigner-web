using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TankDesigner.Core.Models;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    // Servicio encargado de guardar, cargar, duplicar y eliminar proyectos
    // en base de datos. Toda la información del proyecto se serializa en JSON.
    public class ProyectoPersistenciaService
    {
        // Factory para crear DbContext de forma controlada (muy importante en Blazor Server)
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public ProyectoPersistenciaService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        // Guarda un proyecto:
        // - Si tiene Id → actualiza
        // - Si no tiene → crea uno nuevo
        public async Task<int> GuardarAsync(
            string usuarioId,
            int? proyectoId,
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas,
            InstalacionModel instalacion,
            ResultadoCalculoModel resultado)
        {
            // Se crea un DbContext nuevo para esta operación
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            ProyectoEntidad entidad;

            // CASO 1: El proyecto ya existe → actualizar
            if (proyectoId.HasValue)
            {
                entidad = await context.Proyectos
                    .FirstAsync(p => p.Id == proyectoId.Value && p.UsuarioId == usuarioId);

                // Se actualizan los campos principales visibles
                entidad.Nombre = string.IsNullOrWhiteSpace(proyecto.NombreProyecto)
                    ? "Proyecto sin nombre"
                    : proyecto.NombreProyecto.Trim();

                entidad.Cliente = proyecto.ClienteReferencia ?? "";
                entidad.Normativa = proyecto.Normativa ?? "";
                entidad.Fabricante = proyecto.Fabricante ?? "";

                // Se serializa TODO el estado del proyecto en JSON
                entidad.ProyectoJson = JsonConvert.SerializeObject(proyecto);
                entidad.TanqueJson = JsonConvert.SerializeObject(tanque);
                entidad.CargasJson = JsonConvert.SerializeObject(cargas);
                entidad.InstalacionJson = JsonConvert.SerializeObject(instalacion);
                entidad.ResultadoJson = JsonConvert.SerializeObject(resultado);

                // Se actualiza la fecha de modificación
                entidad.FechaModificacion = DateTime.UtcNow;
            }
            // CASO 2: Proyecto nuevo → crear
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

                    // Se guarda todo el estado en JSON
                    ProyectoJson = JsonConvert.SerializeObject(proyecto),
                    TanqueJson = JsonConvert.SerializeObject(tanque),
                    CargasJson = JsonConvert.SerializeObject(cargas),
                    InstalacionJson = JsonConvert.SerializeObject(instalacion),
                    ResultadoJson = JsonConvert.SerializeObject(resultado),

                    // Fechas de control
                    FechaCreacion = DateTime.UtcNow,
                    FechaModificacion = DateTime.UtcNow
                };

                // Se añade a la base de datos
                context.Proyectos.Add(entidad);
            }

            // Guarda cambios en BD
            await context.SaveChangesAsync();

            // Devuelve el Id del proyecto (nuevo o actualizado)
            return entidad.Id;
        }

        // Devuelve la lista de proyectos de un usuario
        public async Task<List<ProyectoEntidad>> ObtenerListaAsync(string usuarioId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Proyectos
                .Where(p => p.UsuarioId == usuarioId) // solo proyectos del usuario
                .OrderByDescending(p => p.FechaModificacion) // más recientes primero
                .ToListAsync();
        }

        // Carga un proyecto desde BD y lo mete en el estado en memoria (ProyectoState)
        public async Task<bool> CargarAsync(string usuarioId, int proyectoId, ProyectoState estado)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Busca el proyecto
            var entidad = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            // Si no existe, devuelve false
            if (entidad == null)
                return false;

            // Se actualiza el estado global de la aplicación
            estado.ProyectoIdActual = entidad.Id;

            // Se deserializan todos los JSON al estado en memoria
            estado.Proyecto = JsonConvert.DeserializeObject<ProyectoGeneralModel>(entidad.ProyectoJson) ?? new();
            estado.Tanque = JsonConvert.DeserializeObject<TankModel>(entidad.TanqueJson) ?? new();
            if (estado.Tanque.AlturaPanelBase < 1000)
            {
                estado.Tanque.AlturaPanelBase = 1200;
            }
            estado.Cargas = JsonConvert.DeserializeObject<CargasModel>(entidad.CargasJson) ?? new();
            estado.Instalacion = JsonConvert.DeserializeObject<InstalacionModel>(entidad.InstalacionJson) ?? new();
            estado.Resultado = JsonConvert.DeserializeObject<ResultadoCalculoModel>(entidad.ResultadoJson) ?? new();

            return true;
        }

        // Duplica un proyecto existente creando uno nuevo con los mismos datos
        public async Task<int?> DuplicarAsync(string usuarioId, int proyectoId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Busca el proyecto original
            var entidadOriginal = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            // Si no existe, devuelve null
            if (entidadOriginal == null)
                return null;

            // Crea una copia con los mismos datos
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

            // Guarda la copia
            context.Proyectos.Add(copia);
            await context.SaveChangesAsync();

            return copia.Id;
        }

        // Elimina un proyecto del usuario
        public async Task<bool> EliminarAsync(string usuarioId, int proyectoId)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();

            // Busca el proyecto
            var entidad = await context.Proyectos
                .FirstOrDefaultAsync(p => p.Id == proyectoId && p.UsuarioId == usuarioId);

            // Si no existe, devuelve false
            if (entidad == null)
                return false;

            // Elimina el proyecto
            context.Proyectos.Remove(entidad);

            // Guarda cambios
            await context.SaveChangesAsync();

            return true;
        }
    }
}