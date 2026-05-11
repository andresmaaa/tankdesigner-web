using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de calcular la geometría básica del tanque.
    // Todas las alturas/ancho de panel se obtienen desde los JSON del fabricante activo.
    public class CalculoGeometriaService
    {
        private const double AlturaFallbackMm = 1200.0;

        private readonly CalculoTanqueService _calculoTanqueService;
        private readonly JsonCatalogService _jsonCatalogService;

        public CalculoGeometriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
            _jsonCatalogService = new JsonCatalogService();
        }

        private List<PosiblePlanchaModel> ObtenerPlanchasValidas(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null)
                return new List<PosiblePlanchaModel>();

            return _calculoTanqueService.ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura > 0 && p.Ancho > 0)
                .ToList();
        }

        private PosiblePlanchaModel? ObtenerPlanchaReferencia(ProyectoGeneralModel proyecto)
        {
            var planchas = ObtenerPlanchasValidas(proyecto);

            if (planchas.Count == 0)
                return null;

            // La plancha base debe salir del JSON activo.
            // Para Balmoral cogerá 1200x2450; para Permastore 1400x2682.
            double alturaReferencia = planchas
                .GroupBy(p => Math.Round(p.Altura, 3))
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .First()
                .Key;

            return planchas
                .Where(p => Math.Abs(p.Altura - alturaReferencia) < 0.001)
                .OrderByDescending(p => p.Ancho)
                .ThenBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .FirstOrDefault();
        }

        public double ObtenerAlturaPanelBase(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            var plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha != null && plancha.Altura > 0)
                return Math.Round(plancha.Altura, 3);

            return tanque.AlturaPanelBase > 0
                ? tanque.AlturaPanelBase
                : AlturaFallbackMm;
        }

        public double ObtenerAlturaTotal(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            var alturasCatalogo = GenerarAlturasAnillosDesdeCatalogo(tanque, proyecto);

            if (alturasCatalogo.Count > 0)
                return Math.Round(alturasCatalogo.Sum(), 3);

            double alturaPanel = ObtenerAlturaPanelBase(tanque, proyecto);

            if (alturaPanel <= 0 || tanque.NumeroAnillos <= 0)
                return 0;

            return Math.Round(tanque.NumeroAnillos * alturaPanel, 3);
        }

        public double ObtenerDiametro(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha == null || plancha.Ancho <= 0 || tanque.ChapasPorAnillo <= 0)
                return 0;

            return Math.Round((tanque.ChapasPorAnillo * plancha.Ancho) / Math.PI, 3);
        }

        public List<double> GenerarAlturasAnillosDesdeCatalogo(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            var resultado = new List<double>();

            if (tanque == null || proyecto == null || tanque.NumeroAnillos <= 0)
                return resultado;

            double alturaPanelBase = ObtenerAlturaPanelBase(tanque, proyecto);
            if (alturaPanelBase <= 0)
                return resultado;

            resultado.AddRange(Enumerable.Repeat(alturaPanelBase, tanque.NumeroAnillos));

            // El anillo 1 es el superior. Si se selecciona 1/2 o 1/4 anillo,
            // la primera altura sale del JSON del fabricante, no de un valor fijo.
            double alturaAnilloSuperior = ObtenerAlturaAnilloSuperiorDesdeCatalogo(proyecto, tanque.TipoAnilloSuperior, alturaPanelBase);
            if (alturaAnilloSuperior > 0 && resultado.Count > 0)
                resultado[0] = alturaAnilloSuperior;

            // El starter ring también sale del JSON. Se aplica solo si existe una posición válida.
            double alturaStarterRing = ObtenerAlturaStarterRingDesdeCatalogo(proyecto);
            if (alturaStarterRing > 0 && tanque.AnilloArranque > 0 && tanque.AnilloArranque <= resultado.Count)
                resultado[tanque.AnilloArranque - 1] = alturaStarterRing;

            return resultado.Select(a => Math.Round(a, 3)).ToList();
        }

        private double ObtenerAlturaAnilloSuperiorDesdeCatalogo(
            ProyectoGeneralModel proyecto,
            string? tipoAnilloSuperior,
            double alturaPanelBase)
        {
            string tipo = (tipoAnilloSuperior ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(tipo) || tipo.Contains("ENTERO"))
                return alturaPanelBase;

            double factor = 1.0;

            if (tipo.Contains("1/2") || tipo.Contains("MEDIO"))
                factor = 0.5;
            else if (tipo.Contains("1/4") || tipo.Contains("CUARTO"))
                factor = 0.25;
            else
                return alturaPanelBase;

            double objetivo = alturaPanelBase * factor;

            var planchas = ObtenerPlanchasValidas(proyecto)
                .Where(p => p.Altura > 0 && p.Altura < alturaPanelBase - 0.001)
                .ToList();

            if (planchas.Count == 0)
                return alturaPanelBase;

            return planchas
                .GroupBy(p => Math.Round(p.Altura, 3))
                .OrderBy(g => Math.Abs(g.Key - objetivo))
                .ThenByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        private double ObtenerAlturaStarterRingDesdeCatalogo(ProyectoGeneralModel proyecto)
        {
            try
            {
                var starterRings = _jsonCatalogService.CargarStarterRings(proyecto.Fabricante);

                return starterRings
                    .Where(sr => sr != null && sr.Altura > 0)
                    .OrderBy(sr => sr.Altura)
                    .Select(sr => sr.Altura)
                    .FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        public bool AlturasAnillosSonValidasParaCatalogo(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return false;

            var alturasCatalogo = GenerarAlturasAnillosDesdeCatalogo(tanque, proyecto);
            return alturasCatalogo.Count == tanque.NumeroAnillos && alturasCatalogo.All(a => a > 0);
        }
    }
}
