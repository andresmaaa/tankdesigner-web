using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Reglas base para normativa EC.
    // Se deja lista para completar fórmulas más adelante.
    public class EcFormulaService : INormativaFormulaService
    {
        public string ObtenerNombreNormativa(string normativa)
        {
            return "EC";
        }

        public double ObtenerCoeficienteEspesor(string normativa)
        {
            return 1.10;
        }

        public double AjustarTensionAdmisible(double tensionAdmisibleBase, string normativa)
        {
            if (tensionAdmisibleBase <= 0)
                return 0;

            return tensionAdmisibleBase * 0.95;
        }

        public double ObtenerCoeficienteSeguridadGeneral(string normativa)
        {
            return 1.0;
        }

        public double ObtenerCoeficienteSeguridadTornilleria(string normativa)
        {
            return 1.0;
        }

        public void AplicarParametrosAnillo(ResultadoAnilloModel resultado)
        {
            if (resultado == null)
                return;

            resultado.ResultadoNormativa.Normativa = "EC";
            resultado.ResultadoNormativa.CoeficienteGlobal = 1.0;
        }
    }
}