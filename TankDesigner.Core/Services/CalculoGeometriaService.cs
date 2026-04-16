using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de calcular la geometría básica del tanque.
    // Se usa para obtener altura, diámetro y panel base cuando no están definidos.
    public class CalculoGeometriaService
    {
        private readonly CalculoTanqueService _calculoTanqueService;
        private const double AlturaPanelBasePreferidaMm = 1200.0;

        public CalculoGeometriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        // Obtiene la plancha de referencia para la geometría base.
        // Prioriza la altura estándar de 1200 mm cuando exista en el catálogo filtrado.
        // Si no existe, usa la plancha válida de mayor altura como respaldo.
        private PosiblePlanchaModel? ObtenerPlanchaBaseGeometrica(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null)
                return null;

            var planchasValidas = _calculoTanqueService.ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura > 0 && p.Ancho > 0)
                .ToList();

            if (planchasValidas.Count == 0)
                return null;

            var planchaPreferida = planchasValidas
                .Where(p => Math.Abs(p.Altura - AlturaPanelBasePreferidaMm) < 0.001)
                .OrderBy(p => p.Ancho)
                .FirstOrDefault();

            if (planchaPreferida != null)
                return planchaPreferida;

            return planchasValidas
                .OrderByDescending(p => p.Altura)
                .ThenBy(p => p.Ancho)
                .FirstOrDefault();
        }

        // Devuelve la altura del panel base.
        // Se toma directamente de la plancha geométrica de referencia.
        public double ObtenerAlturaPanelBase(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaBaseGeometrica(proyecto);

            if (plancha == null)
                return 0;

            return plancha.Altura;
        }

        // Calcula la altura total del tanque.
        // Se basa en número de anillos y altura de cada panel.
        public double ObtenerAlturaTotal(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            double alturaPanel = ObtenerAlturaPanelBase(tanque, proyecto);

            if (alturaPanel <= 0 || tanque.NumeroAnillos <= 0)
                return 0;

            return tanque.NumeroAnillos * alturaPanel;
        }

        // Calcula el diámetro del tanque.
        // Usa el ancho de la plancha geométrica de referencia y el número de chapas por anillo.
        public double ObtenerDiametro(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaBaseGeometrica(proyecto);

            if (plancha == null)
                return 0;

            if (plancha.Ancho <= 0 || tanque.ChapasPorAnillo <= 0)
                return 0;

            // Fórmula: perímetro ≈ chapas * ancho → diámetro = perímetro / π
            return (tanque.ChapasPorAnillo * plancha.Ancho) / Math.PI;
        }
    }
}
