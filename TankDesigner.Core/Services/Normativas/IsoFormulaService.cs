using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Reglas base para normativa ISO.
    // De momento deja preparado el comportamiento para completarlo después.
    public class IsoFormulaService : INormativaFormulaService
    {
        public string ObtenerNombreNormativa(string normativa)
        {
            return "ISO";
        }

        public double ObtenerCoeficienteEspesor(string normativa)
        {
            return 1.05;
        }

        public double AjustarTensionAdmisible(double tensionAdmisibleBase, string normativa)
        {
            if (tensionAdmisibleBase <= 0)
                return 0;

            return tensionAdmisibleBase * 0.98;
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

            resultado.ResultadoNormativa.Normativa = "ISO";
            resultado.ResultadoNormativa.CoeficienteGlobal = 1.0;
        }
    }
}