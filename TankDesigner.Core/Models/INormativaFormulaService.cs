using TankDesigner.Core.Models;

namespace TankDesigner.Core.Interfaces
{
    // Contrato base para aplicar reglas específicas de cada normativa.
    // Se usa para no dejar lógica AWWA, ISO o EC metida dentro del cálculo general.
    public interface INormativaFormulaService
    {
        string ObtenerNombreNormativa(string normativa);
        double ObtenerCoeficienteEspesor(string normativa);
        double AjustarTensionAdmisible(double tensionAdmisibleBase, string normativa);
        double ObtenerCoeficienteSeguridadGeneral(string normativa);
        double ObtenerCoeficienteSeguridadTornilleria(string normativa);

        void AplicarParametrosAnillo(ResultadoAnilloModel resultado);
    }
}