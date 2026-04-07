using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Reglas específicas de la normativa AWWA.
    public class AwwaFormulaService : INormativaFormulaService
    {
        public string ObtenerNombreNormativa(string normativa)
        {
            return "AWWA";
        }

        public double ObtenerCoeficienteEspesor(string normativa)
        {
            return 1.00;
        }

        public double AjustarTensionAdmisible(double tensionAdmisibleBase, string normativa)
        {
            if (tensionAdmisibleBase <= 0)
                return 0;

            return tensionAdmisibleBase;
        }

        public double ObtenerCoeficienteSeguridadGeneral(string normativa)
        {
            return 1.5;
        }

        public double ObtenerCoeficienteSeguridadTornilleria(string normativa)
        {
            return 1.2;
        }

        public void AplicarParametrosAnillo(ResultadoAnilloModel resultado)
        {
            if (resultado == null)
                return;

            resultado.ResultadoNormativa.Normativa = "AWWA";
            resultado.ResultadoNormativa.CoeficienteGlobal = 1.5;
            resultado.ResultadoNormativa.CoeficienteViento = resultado.AwwaWindFactor;
            resultado.ResultadoNormativa.CoeficienteCubierta = resultado.AwwaRoofFactor;
            resultado.ResultadoNormativa.CoeficienteSismo = resultado.AwwaSeismicFactor;
        }
    }
}