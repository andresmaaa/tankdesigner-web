using TankDesigner.Core.Services;
using TankDesigner.Infrastructure.Services;

namespace TankDesigner.Web.Services
{
    public class OpcionesFormularioService
    {
        private readonly CatalogoJsonService _catalogoJsonService;
        private readonly JsonCatalogService _jsonCatalogService = new();

        public OpcionesFormularioService(CatalogoJsonService catalogoJsonService)
        {
            _catalogoJsonService = catalogoJsonService;
        }

        public List<string> ObtenerModelosCalculo()
            => new() { "Simple", "Extendido" };

        public List<string> ObtenerNormativas()
            => new() { "AWWA D103-19", "ISO", "EC" };

        // El orden prioriza los fabricantes reales que usa el programa base.
        public List<string> ObtenerFabricantes()
            => new() { "Permastore", "Balmoral", "DL2" };

        public List<string> ObtenerMateriales(string fabricante)
        {
            var materialesOcultos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "S235",
                "S275",
                "S355",
                "HSLA4",
                "HSLA5"
             };

            return _jsonCatalogService.CargarPlanchas(fabricante)
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Material))
                .Select(x => x.Material.Trim())
                .Where(x => !materialesOcultos.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        public List<string> ObtenerMateriales()
            => ObtenerMateriales("PERMASTORE");
        public List<int> ObtenerChapasPorAnillo()
            => new() { 16, 14, 12, 10, 8 };

        public List<int> ObtenerNumeroAnillos()
            => new() { 6, 5, 4, 3 };

        public List<int> ObtenerAnillosArranque()
            => new() { 1, 2, 3 };

        public List<string> ObtenerModelosPorFabricante(string fabricante)
            => _catalogoJsonService.ObtenerModelosDisponibles(fabricante);

        public List<string> ObtenerConfiguracionesPorFabricante(string fabricante)
            => _catalogoJsonService.ObtenerModelosDisponibles(fabricante);

        public List<string> ObtenerRoofTypes()
            => new() { "None", "Conical", "Dome", "Flat" };

        // En el programa base el ángulo de techo no se trata como un rango 0-100,
        // sino como una lista cerrada de opciones técnicas.
        public List<string> ObtenerRoofAngles()
            => new() { "0°", "5°", "10°", "15°", "20°", "30°", "45°" };
        public List<string> ObtenerAngulosSuperiores(string fabricante)
        {
            return _catalogoJsonService
                .ObtenerRigidizadoresSuperiores(fabricante)
                .Select(x => !string.IsNullOrWhiteSpace(x.Nombre) ? x.Nombre : x.Tipo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
        }
        public List<string> ObtenerClasesExposicion()
            => new() { "A", "B", "C", "D" };

        public List<string> ObtenerSiteClasses()
            => new() { "A", "B", "C", "D", "E", "F" };

        public List<string> ObtenerSeismicUseGroups()
            => new() { "I", "II", "III" };

        public List<string> ObtenerTiposMedioAnillo()
            => new() { "Anillo entero", "1/2 anillo", "1/4 anillo" };

        public List<string> ObtenerOpcionesStarterRing()
            => new() { "Sí", "No" };

        public List<string> ObtenerTiposTechoInstalacion()
            => new() { "Sin techo", "Cónico", "Plano", "Domo geodésico" };

        public List<string> ObtenerTiposEscalera()
            => new() { "Sin escalera", "Vertical", "Helicoidal" };

        public List<string> ObtenerLugaresObra()
            => new() { "Nacional", "Europa", "Internacional" };
    }
}
