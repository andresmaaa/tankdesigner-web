using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de seleccionar un starter ring desde el catálogo.
    // Busca la opción más adecuada según los datos del tanque.
    public class CalculoStarterRingService
    {
        // Devuelve el starter ring base que mejor encaja con el tanque.
        public PosibleStarterRingModel ObtenerStarterRingBase(CalculoTanqueInputModel input)
        {
            // Si no hay datos de entrada, no se puede calcular.
            if (input == null)
                return null;

            // Carga todos los starter rings disponibles del catálogo.
            var jsonCatalogService = new JsonCatalogService();
            var starterRings = jsonCatalogService.CargarStarterRings(input.Fabricante);

            if (starterRings == null || starterRings.Count == 0)
                return null;

            // Filtra por fabricante (si el catálogo lo especifica).
            var starterRingsFabricante = starterRings
                .Where(sr => sr != null)
                .Where(sr =>
                    string.IsNullOrWhiteSpace(sr.Fabricante) ||
                    sr.Fabricante.Equals(input.Fabricante, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Si no hay coincidencias, usa todos los del catálogo.
            if (starterRingsFabricante.Count == 0)
                starterRingsFabricante = starterRings;

            // Altura del panel base del tanque, usada como referencia.
            double alturaObjetivo = input.AlturaPanelBase;

            // Selecciona el starter ring cuya altura sea más cercana a la requerida.
            var starterRingSeleccionado = starterRingsFabricante
                .OrderBy(sr => Math.Abs(sr.Altura - alturaObjetivo))
                .FirstOrDefault();

            return starterRingSeleccionado;
        }
    }
}