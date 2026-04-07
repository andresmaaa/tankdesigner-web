using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services
{
    public class FormulaRigidizadorService
    {
        // Calcula la altura mínima del rigidizador usando datos reales del tanque
        public double ObtenerAlturaMinimaRigidizador(ResultadoCalculoModel resultado)
        {
            if (resultado == null)
                return 0;

            if (resultado.AlturaTotal > 0)
                return resultado.AlturaTotal / 1000.0;

            return 0;
        }

        // Calcula el espesor mínimo del rigidizador según el mayor espesor real de anillo
        public double ObtenerEspesorMinimoRigidizador(ResultadoCalculoModel resultado)
        {
            if (resultado == null || resultado.Anillos == null || resultado.Anillos.Count == 0)
                return 0;

            double espesorMaximoSeleccionado = resultado.Anillos.Max(a => a.EspesorSeleccionado);

            if (espesorMaximoSeleccionado <= 0)
                return 0;

            return espesorMaximoSeleccionado;
        }
    }
}
