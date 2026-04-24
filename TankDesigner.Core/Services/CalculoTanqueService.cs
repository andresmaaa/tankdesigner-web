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

        public List<PosiblePlanchaModel> ObtenerPlanchasDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosiblePlanchaModel>();

            return _jsonCatalogService.CargarPlanchas(proyecto.Fabricante)
                .Where(p => p != null)
                .ToList();
        }

        public List<PosiblePlanchaModel> ObtenerPlanchasFiltradas(ProyectoGeneralModel proyecto)
        {
            var todas = ObtenerPlanchasDisponibles(proyecto);
            if (todas == null || !todas.Any())
                return new List<PosiblePlanchaModel>();

            string materialBuscado = (proyecto.MaterialPrincipal ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(materialBuscado))
            {
                return todas
                    .Where(p => p.Altura > 0 && p.Ancho > 0)
                    .ToList();
            }

            var filtradas = todas
                .Where(p =>
                    (!string.IsNullOrWhiteSpace(p.Material) && p.Material.ToUpperInvariant() == materialBuscado)
                    || (!string.IsNullOrWhiteSpace(p.Acabado) && p.Acabado.ToUpperInvariant() == materialBuscado))
                .ToList();

            if (filtradas.Count == 0 || filtradas.Count < 3)
            {
                return todas
                    .Where(p => p.Altura > 0 && p.Ancho > 0)
                    .ToList();
            }

            return filtradas;
        }

        public List<PosiblePlanchaModel> ObtenerPlanchasPorAltura(ProyectoGeneralModel proyecto, double alturaPanel)
        {
            return ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura == alturaPanel)
                .ToList();
        }

        public List<PosiblePlanchaModel> ObtenerPlanchasOrdenadasPorResistencia(ProyectoGeneralModel proyecto, double alturaPanel)
        {
            return ObtenerPlanchasPorAltura(proyecto, alturaPanel)
                .Where(p => p != null && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ToList();
        }

        public List<PosibleConfiguracionModel> ObtenerConfiguracionesDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosibleConfiguracionModel>();

            return _jsonCatalogService.CargarConfiguraciones(proyecto.Fabricante)
                .Where(c => c != null)
                .ToList();
        }

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

        public List<PosibleTornilloModel> ObtenerTornillosDisponibles(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null || string.IsNullOrWhiteSpace(proyecto.Fabricante))
                return new List<PosibleTornilloModel>();

            return _jsonCatalogService.CargarTornillos(proyecto.Fabricante)
                .Where(t => t != null)
                .ToList();
        }

        public List<PosibleTornilloModel> ObtenerTornillosOrdenados(ProyectoGeneralModel proyecto)
        {
            return ObtenerTornillosDisponibles(proyecto)
                .OrderBy(t => t.Diametro)
                .ThenBy(t => t.FyTornillos)
                .ThenBy(t => t.FuTornillos)
                .ToList();
        }

        public List<PosiblePlanchaModel> ObtenerPlanchasCandidatasOrdenadas(ProyectoGeneralModel proyecto, double alturaPanel)
        {
            return ObtenerPlanchasPorAltura(proyecto, alturaPanel)
                .Where(p => p != null && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ThenBy(p => p.Espesor.Min())
                .ToList();
        }

        // Devuelve planchas candidatas ajustadas al material y altura reales de un anillo.
        public List<PosiblePlanchaModel> ObtenerPlanchasCandidatasOrdenadasPorAnillo(
            ProyectoGeneralModel proyecto,
            double alturaPanel,
            string materialAnillo)
        {
            if (proyecto == null)
                return new List<PosiblePlanchaModel>();

            var proyectoAnillo = new ProyectoGeneralModel
            {
                Fabricante = proyecto.Fabricante,
                MaterialPrincipal = string.IsNullOrWhiteSpace(materialAnillo)
                    ? string.Empty
                    : materialAnillo
            };

            var exactas = ObtenerPlanchasPorAltura(proyectoAnillo, alturaPanel)
                .Where(p => p != null && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ThenBy(p => p.Espesor.Min())
                .ToList();

            if (exactas.Count > 0)
                return exactas;

            return ObtenerPlanchasFiltradas(proyectoAnillo)
                .Where(p => p != null && p.Altura > 0 && p.Espesor != null && p.Espesor.Count > 0)
                .OrderBy(p => Math.Abs(p.Altura - alturaPanel))
                .ThenBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .ThenBy(p => p.Espesor.Min())
                .ToList();
        }

        public List<PosibleConfiguracionModel> ObtenerConfiguracionesOrdenadasPorAnillo(
            ProyectoGeneralModel proyecto,
            string configuracionPreferida)
        {
            var configuraciones = ObtenerConfiguracionesOrdenadas(proyecto);

            if (string.IsNullOrWhiteSpace(configuracionPreferida))
                return configuraciones;

            string nombre = configuracionPreferida.Trim();

            return configuraciones
                .OrderBy(c => !string.IsNullOrWhiteSpace(c.Nombre) && c.Nombre.Trim().Equals(nombre, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(c => c.S)
                .ThenByDescending(c => c.R)
                .ThenBy(c => c.NumeroTornillosUnionVertical)
                .ThenBy(c => c.NumeroTornillosUnionHorizontalCalculo)
                .ThenBy(c => c.DiametroAgujero)
                .ToList();
        }
    }
}
