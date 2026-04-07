using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    public class CalculoConfiguracionService
    {
        private readonly CalculoTanqueService _calculoTanqueService;

        public CalculoConfiguracionService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        // Busca la configuración que mejor encaja con el modelo del tanque
        public PosibleConfiguracionModel ObtenerConfiguracionValida(CalculoTanqueInputModel input)
        {
            if (input == null)
                return null;

            // Se crea un objeto proyecto con el fabricante
            var proyecto = new ProyectoGeneralModel
            {
                Fabricante = input.Fabricante
            };

            // Carga todas las configuraciones disponibles
            var configuraciones = _calculoTanqueService.ObtenerConfiguracionesDisponibles(proyecto);

            if (configuraciones == null || configuraciones.Count == 0)
                return null;

            // Normaliza el nombre del modelo (quita espacios, mayúsculas, etc.)
            string modeloBuscadoNormalizado = NormalizarTexto(input.Modelo);

            if (string.IsNullOrWhiteSpace(modeloBuscadoNormalizado))
                return null;

            // Filtra por fabricante
            var configuracionesFiltradas = configuraciones
                .Where(c => c != null)
                .Where(c =>
                    string.IsNullOrWhiteSpace(c.Fabricante) ||
                    string.Equals(c.Fabricante, input.Fabricante, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (configuracionesFiltradas.Count == 0)
                configuracionesFiltradas = configuraciones;

            // Busca coincidencia exacta
            var coincidenciaExacta = configuracionesFiltradas
                .FirstOrDefault(c =>
                    string.Equals(
                        NormalizarTexto(c.Nombre),
                        modeloBuscadoNormalizado,
                        StringComparison.OrdinalIgnoreCase));

            if (coincidenciaExacta != null)
                return coincidenciaExacta;

            // Si no hay exacta, busca coincidencia parcial
            var coincidenciaParcial = configuracionesFiltradas
                .FirstOrDefault(c =>
                    NormalizarTexto(c.Nombre).Contains(modeloBuscadoNormalizado) ||
                    modeloBuscadoNormalizado.Contains(NormalizarTexto(c.Nombre)));

            return coincidenciaParcial;
        }

        // Limpia el texto para comparar mejor
        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            return texto
                .Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .ToUpperInvariant();
        }
    }
}
