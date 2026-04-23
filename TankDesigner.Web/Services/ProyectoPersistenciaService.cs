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

            NormalizarEstadoCargado(estado);

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

        private static void NormalizarEstadoCargado(ProyectoState estado)
        {
            estado.Proyecto ??= new ProyectoGeneralModel();
            estado.Tanque ??= new TankModel();
            estado.Cargas ??= new CargasModel();
            estado.Instalacion ??= new InstalacionModel();
            estado.Resultado ??= new ResultadoCalculoModel();

            if (estado.Tanque.ChapasPorAnillo <= 0)
                estado.Tanque.ChapasPorAnillo = 16;

            if (estado.Tanque.NumeroAnillos <= 0)
                estado.Tanque.NumeroAnillos = estado.Resultado.NumeroAnillos > 0 ? estado.Resultado.NumeroAnillos : 6;

            if (estado.Tanque.AnilloArranque <= 0)
                estado.Tanque.AnilloArranque = 1;

            if (estado.Tanque.AlturaPanelBase <= 0)
                estado.Tanque.AlturaPanelBase = 1200;

            estado.Tanque.AlturasAnillos ??= new List<double>();
            estado.Tanque.MaterialesAnillos ??= new List<string>();
            estado.Tanque.ConfiguracionesAnillos ??= new List<string>();

            if (estado.Tanque.AlturasAnillos.Count == 0 && estado.Resultado.Anillos != null && estado.Resultado.Anillos.Count > 0)
            {
                estado.Tanque.AlturasAnillos = estado.Resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(x =>
                    {
                        double altura = x.AlturaSuperior - x.AlturaInferior;
                        return altura > 0 ? altura : estado.Tanque.AlturaPanelBase;
                    })
                    .ToList();
            }

            if (estado.Tanque.MaterialesAnillos.Count == 0 && estado.Resultado.Anillos != null && estado.Resultado.Anillos.Count > 0)
            {
                estado.Tanque.MaterialesAnillos = estado.Resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(x => (x.MaterialAplicado ?? string.Empty).Trim())
                    .ToList();
            }

            if (estado.Tanque.ConfiguracionesAnillos.Count == 0 && estado.Resultado.Anillos != null && estado.Resultado.Anillos.Count > 0)
            {
                estado.Tanque.ConfiguracionesAnillos = estado.Resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(x => (x.ConfiguracionAplicada ?? string.Empty).Trim())
                    .ToList();
            }

            string materialDefault = !string.IsNullOrWhiteSpace(estado.Proyecto.MaterialPrincipal)
                ? estado.Proyecto.MaterialPrincipal.Trim()
                : "S235";

            while (estado.Tanque.AlturasAnillos.Count < estado.Tanque.NumeroAnillos)
                estado.Tanque.AlturasAnillos.Add(estado.Tanque.AlturaPanelBase > 0 ? estado.Tanque.AlturaPanelBase : 1200);

            while (estado.Tanque.MaterialesAnillos.Count < estado.Tanque.NumeroAnillos)
                estado.Tanque.MaterialesAnillos.Add(materialDefault);

            while (estado.Tanque.ConfiguracionesAnillos.Count < estado.Tanque.NumeroAnillos)
                estado.Tanque.ConfiguracionesAnillos.Add(string.Empty);

            if (estado.Tanque.AlturasAnillos.Count > estado.Tanque.NumeroAnillos)
                estado.Tanque.AlturasAnillos = estado.Tanque.AlturasAnillos.Take(estado.Tanque.NumeroAnillos).ToList();

            if (estado.Tanque.MaterialesAnillos.Count > estado.Tanque.NumeroAnillos)
                estado.Tanque.MaterialesAnillos = estado.Tanque.MaterialesAnillos.Take(estado.Tanque.NumeroAnillos).ToList();

            if (estado.Tanque.ConfiguracionesAnillos.Count > estado.Tanque.NumeroAnillos)
                estado.Tanque.ConfiguracionesAnillos = estado.Tanque.ConfiguracionesAnillos.Take(estado.Tanque.NumeroAnillos).ToList();

            for (int i = 0; i < estado.Tanque.NumeroAnillos; i++)
            {
                if (estado.Tanque.AlturasAnillos[i] <= 0)
                    estado.Tanque.AlturasAnillos[i] = estado.Tanque.AlturaPanelBase > 0 ? estado.Tanque.AlturaPanelBase : 1200;

                if (string.IsNullOrWhiteSpace(estado.Tanque.MaterialesAnillos[i]))
                    estado.Tanque.MaterialesAnillos[i] = materialDefault;
            }
        }
    }
}
