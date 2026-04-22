using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de calcular la geometría básica del tanque.
    // Toma como referencia el catálogo real del fabricante.
    public class CalculoGeometriaService
    {
        private const double AlturaPanelEstandarMm = 1200.0;

        private readonly CalculoTanqueService _calculoTanqueService;

        public CalculoGeometriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        // Devuelve las planchas válidas del fabricante ya filtradas por material.
        private List<PosiblePlanchaModel> ObtenerPlanchasValidas(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null)
                return new List<PosiblePlanchaModel>();

            return _calculoTanqueService.ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura > 0 && p.Ancho > 0)
                .ToList();
        }

        // Busca primero la altura estándar del programa base.
        // Si no existe para el catálogo activo, usa una plancha válida de respaldo.
        private PosiblePlanchaModel? ObtenerPlanchaReferencia(ProyectoGeneralModel proyecto)
        {
            List<PosiblePlanchaModel> planchas = _jsonCatalogService.ObtenerPlanchas(proyecto);

            if (planchas == null || planchas.Count == 0)
                return null;

            // Prioridad 1: la clásica de Balmoral
            PosiblePlanchaModel? plancha1200 = planchas
                .Where(p => p != null)
                .FirstOrDefault(p => Math.Abs(p.Altura - 1200) < 0.01);

            if (plancha1200 != null)
                return plancha1200;

            // Prioridad 2: la altura completa más grande del catálogo
            PosiblePlanchaModel? planchaBase = planchas
                .Where(p => p != null && p.Altura > 700)
                .OrderByDescending(p => p.Altura)
                .FirstOrDefault();

            if (planchaBase != null)
                return planchaBase;

            // Último recurso: la mayor altura disponible
            return planchas
                .Where(p => p != null)
                .OrderByDescending(p => p.Altura)
                .FirstOrDefault();
        }

        // Devuelve la altura del panel base siguiendo la lógica del programa base.
        public double ObtenerAlturaPanelBase(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha == null || plancha.Altura <= 0)
                return 0;

            return plancha.Altura;
        }


        // Calcula la altura total del tanque usando la altura base seleccionada.
        public double ObtenerAlturaTotal(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            double alturaPanel = ObtenerAlturaPanelBase(tanque, proyecto);

            if (alturaPanel <= 0 || tanque.NumeroAnillos <= 0)
                return 0;

            return tanque.NumeroAnillos * alturaPanel;
        }

        // Calcula el diámetro con el ancho de la plancha de referencia.
        public double ObtenerDiametro(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha == null || plancha.Ancho <= 0 || tanque.ChapasPorAnillo <= 0)
                return 0;

            return (tanque.ChapasPorAnillo * plancha.Ancho) / Math.PI;
        }
    }
}
