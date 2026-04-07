using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de seleccionar el rigidizador adecuado.
    // Usa catálogo + fórmulas para filtrar y elegir la mejor opción.
    public class CalculoRigidizadoresService
    {
        private readonly JsonCatalogService _jsonCatalogService;
        private readonly FormulaRigidizadorService _formulaRigidizadorService;

        public CalculoRigidizadoresService()
        {
            _jsonCatalogService = new JsonCatalogService();
            _formulaRigidizadorService = new FormulaRigidizadorService();
        }

        // Devuelve el rigidizador base más adecuado según el resultado del cálculo.
        public PosibleRigidizadorModel ObtenerRigidizadorBase(CalculoTanqueInputModel input, ResultadoCalculoModel resultado)
        {
            // Si no hay datos, no se puede seleccionar.
            if (input == null)
                return null;

            // Carga los rigidizadores del catálogo.
            var rigidizadores = _jsonCatalogService.CargarRigidizadores(input.Fabricante);

            if (rigidizadores == null || rigidizadores.Count == 0)
                return null;

            // Filtra por fabricante si está definido.
            var rigidizadoresFabricante = rigidizadores
                .Where(r => r != null)
                .Where(r =>
                    string.IsNullOrWhiteSpace(r.Fabricante) ||
                    r.Fabricante.Equals(input.Fabricante, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Si no hay coincidencias, usa todos.
            if (rigidizadoresFabricante.Count == 0)
                rigidizadoresFabricante = rigidizadores;

            // Obtiene los mínimos necesarios desde las fórmulas.
            double alturaMinima = _formulaRigidizadorService.ObtenerAlturaMinimaRigidizador(resultado);
            double espesorMinimo = _formulaRigidizadorService.ObtenerEspesorMinimoRigidizador(resultado);

            // Filtra los que cumplen condiciones mínimas y los ordena.
            var candidatosValidos = rigidizadoresFabricante
                .Where(r =>
                    (alturaMinima <= 0 || r.Altura >= alturaMinima) &&
                    (espesorMinimo <= 0 || r.Espesor >= espesorMinimo))
                .OrderBy(r => r.Peso)
                .ThenBy(r => r.Precio)
                .ThenBy(r => r.Altura)
                .ToList();

            // Devuelve el mejor candidato (el primero tras ordenar).
            if (candidatosValidos.Count > 0)
                return candidatosValidos.First();

            // Si ninguno cumple, no hay rigidizador válido.
            return null;
        }
    }
}