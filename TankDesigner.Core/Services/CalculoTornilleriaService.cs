using System;
using System.Collections.Generic;
using System.Linq;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio que busca y ordena los tornillos del catálogo.
    public class CalculoTornilleriaService
    {
        private readonly CalculoTanqueService _calculoTanqueService;

        public CalculoTornilleriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        // Devuelve un tornillo base según la configuración elegida.
        public PosibleTornilloModel ObtenerTornilloBase(CalculoTanqueInputModel input, PosibleConfiguracionModel configuracion)
        {
            if (input == null || configuracion == null)
                return null;

            return ObtenerTornillosOrdenadosPorCercania(input, configuracion)
                .FirstOrDefault();
        }

        // Devuelve los tornillos ordenados por cercanía al diámetro de agujero de la configuración.
        public List<PosibleTornilloModel> ObtenerTornillosOrdenadosPorCercania(
            CalculoTanqueInputModel input,
            PosibleConfiguracionModel configuracion)
        {
            if (input == null || configuracion == null)
                return new List<PosibleTornilloModel>();

            var proyecto = new ProyectoGeneralModel
            {
                Fabricante = input.Fabricante,
                MaterialPrincipal = input.MaterialPrincipal
            };

            var tornillos = _calculoTanqueService.ObtenerTornillosOrdenados(proyecto);

            if (tornillos == null || tornillos.Count == 0)
                return new List<PosibleTornilloModel>();

            return tornillos
                .Where(t => t != null)
                .OrderBy(t => Math.Abs(t.Diametro - configuracion.DiametroAgujero))
                .ThenBy(t => t.Diametro)
                .ThenBy(t => t.FuTornillos)
                .ToList();
        }

        // Devuelve el siguiente tornillo disponible respecto al actual.
        public PosibleTornilloModel ObtenerSiguienteTornillo(
            CalculoTanqueInputModel input,
            PosibleConfiguracionModel configuracion,
            PosibleTornilloModel tornilloActual)
        {
            var tornillos = ObtenerTornillosOrdenadosPorCercania(input, configuracion);

            if (tornillos.Count == 0)
                return null;

            if (tornilloActual == null)
                return tornillos.FirstOrDefault();

            int indiceActual = tornillos.FindIndex(t =>
                t != null &&
                t.Diametro == tornilloActual.Diametro &&
                string.Equals(t.CalidadTornillo, tornilloActual.CalidadTornillo, StringComparison.OrdinalIgnoreCase));

            if (indiceActual < 0)
                return tornillos.FirstOrDefault();

            if (indiceActual + 1 >= tornillos.Count)
                return null;

            return tornillos[indiceActual + 1];
        }
    }
}