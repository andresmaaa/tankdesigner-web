using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de calcular la geometría básica del tanque.
    // Se usa para obtener altura, diámetro y panel base cuando no están definidos.
    public class CalculoGeometriaService
    {
        private readonly CalculoTanqueService _calculoTanqueService;

        public CalculoGeometriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        // Obtiene la primera plancha válida del catálogo.
        // Se usa como referencia para calcular dimensiones.
        private PosiblePlanchaModel? ObtenerPrimeraPlanchaFiltrada(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null)
                return null;

            var planchas = _calculoTanqueService.ObtenerPlanchasFiltradas(proyecto);

            // Filtra planchas válidas y selecciona la más pequeña como referencia.
            return planchas
                .Where(p => p != null && p.Altura > 0 && p.Ancho > 0)
                .OrderBy(p => p.Altura)
                .ThenBy(p => p.Ancho)
                .FirstOrDefault();
        }

        // Devuelve la altura del panel base.
        // Se toma directamente de la plancha seleccionada.
        public double ObtenerAlturaPanelBase(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPrimeraPlanchaFiltrada(proyecto);

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
        // Usa el ancho de la plancha y el número de chapas por anillo.
        public double ObtenerDiametro(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPrimeraPlanchaFiltrada(proyecto);

            if (plancha == null)
                return 0;

            if (plancha.Ancho <= 0 || tanque.ChapasPorAnillo <= 0)
                return 0;

            // Fórmula: perímetro ≈ chapas * ancho → diámetro = perímetro / π
            return (tanque.ChapasPorAnillo * plancha.Ancho) / Math.PI;
        }
    }
}