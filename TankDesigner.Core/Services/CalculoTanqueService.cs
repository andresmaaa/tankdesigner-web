using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio de apoyo que trabaja con los catálogos.
    // Se encarga de obtener, filtrar y ordenar los datos que luego usa el cálculo.
    public class CalculoTanqueService
    {
        private readonly JsonCatalogService _jsonCatalogService;

        public CalculoTanqueService()
        {
            _jsonCatalogService = new JsonCatalogService();
        }

        // Devuelve todas las planchas disponibles del fabricante.
        public List<PosiblePlanchaModel> ObtenerPlanchasDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosiblePlanchaModel>();

            return _jsonCatalogService.CargarPlanchas(proyecto.Fabricante)
                .Where(p => p != null)
                .ToList();
        }

        // Filtra las planchas según el material seleccionado en el proyecto.
        // Si no encuentra coincidencias, devuelve todas.
        public List<PosiblePlanchaModel> ObtenerPlanchasFiltradas(ProyectoGeneralModel proyecto)
        {
            var todas = ObtenerPlanchasDisponibles(proyecto);
            if (todas == null || !todas.Any())
                return new List<PosiblePlanchaModel>();

            string materialBuscado = (proyecto.MaterialPrincipal ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            // 🔹 1. Intentamos filtrar por material (como ahora)
            var filtradas = todas
                .Where(p =>
                    (!string.IsNullOrWhiteSpace(p.Material) &&
                     p.Material.ToUpperInvariant() == materialBuscado)
                    ||
                    (!string.IsNullOrWhiteSpace(p.Acabado) &&
                     p.Acabado.ToUpperInvariant() == materialBuscado)
                )
                .ToList();

            // 🔹 2. Si no hay suficientes → usamos TODAS (fallback clave)
            if (filtradas.Count == 0)
            {
                return todas
                    .Where(p => p.Altura > 0 && p.Ancho > 0)
                    .ToList();
            }

            // 🔹 3. Si hay pero son muy pocas → también fallback
            if (filtradas.Count < 3)
            {
                return todas
                    .Where(p => p.Altura > 0 && p.Ancho > 0)
                    .ToList();
            }

            return filtradas;
        }

        // Devuelve las planchas que coinciden con una altura concreta de panel.
        public List<PosiblePlanchaModel> ObtenerPlanchasPorAltura(
            ProyectoGeneralModel proyecto,
            double alturaPanel)
        {
            return ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura == alturaPanel)
                .ToList();
        }

        // Ordena las planchas por resistencia (Fy y Fu).
        // Se usa para elegir primero las opciones más adecuadas.
        public List<PosiblePlanchaModel> ObtenerPlanchasOrdenadasPorResistencia(
            ProyectoGeneralModel proyecto,
            double alturaPanel)
        {
            return ObtenerPlanchasPorAltura(proyecto, alturaPanel)
                .Where(p => p != null && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ToList();
        }

        // Devuelve todas las configuraciones disponibles del fabricante.
        public List<PosibleConfiguracionModel> ObtenerConfiguracionesDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosibleConfiguracionModel>();

            return _jsonCatalogService.CargarConfiguraciones(proyecto.Fabricante)
                .Where(c => c != null)
                .ToList();
        }

        // Ordena las configuraciones según sus parámetros técnicos.
        public List<PosibleConfiguracionModel> ObtenerConfiguracionesOrdenadas(ProyectoGeneralModel proyecto)
        {
            return ObtenerConfiguracionesDisponibles(proyecto)
                .OrderBy(c => c.S)
                .ThenByDescending(c => c.R)
                .ThenBy(c => c.NumeroTornillosUnionVertical)
                .ThenBy(c => c.NumeroTornillosUnionHorizontalCalculo)
                .ThenBy(c => c.DiametroAgujero)
                .ToList();
        }

        // Devuelve todos los tornillos disponibles del fabricante.
        public List<PosibleTornilloModel> ObtenerTornillosDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosibleTornilloModel>();

            return _jsonCatalogService.CargarTornillos(proyecto.Fabricante)
                .Where(t => t != null)
                .ToList();
        }

        // Ordena los tornillos por diámetro y propiedades mecánicas.
        public List<PosibleTornilloModel> ObtenerTornillosOrdenados(ProyectoGeneralModel proyecto)
        {
            return ObtenerTornillosDisponibles(proyecto)
                .OrderBy(t => t.Diametro)
                .ThenBy(t => t.FyTornillos)
                .ThenBy(t => t.FuTornillos)
                .ToList();
        }

        // Devuelve planchas candidatas ordenadas para el cálculo.
        // Se tienen en cuenta resistencia y espesores disponibles.
        public List<PosiblePlanchaModel> ObtenerPlanchasCandidatasOrdenadas(
            ProyectoGeneralModel proyecto,
            double alturaPanel)
        {
            return ObtenerPlanchasPorAltura(proyecto, alturaPanel)
                .Where(p => p != null && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ThenBy(p => p.Espesor.Min())
                .ToList();
        }
    }
}