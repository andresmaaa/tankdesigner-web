namespace TankDesigner.Core.Services
{
    // Servicio con fórmulas básicas de presión, espesor y tensión admisible.
    // Las reglas específicas de cada normativa se aplican fuera de esta clase.
    public class FormulaEspesoresService
    {
        // Calcula la presión hidrostática del líquido según la altura.
        public double CalcularPresionHidrostatica(
            double densidadLiquido,
            double alturaLiquidoSobreCentroM)
        {
            if (densidadLiquido <= 0 || alturaLiquidoSobreCentroM <= 0)
                return 0;

            return 9.81 * densidadLiquido * alturaLiquidoSobreCentroM;
        }

        // Calcula el espesor requerido de la chapa.
        public double CalcularEspesorRequerido(
            double presionKPa,
            double diametroM,
            double tensionAdmisibleMPa)
        {
            if (presionKPa <= 0 || diametroM <= 0 || tensionAdmisibleMPa <= 0)
                return 0;

            double presionMPa = presionKPa / 1000.0;
            double espesorM = (presionMPa * diametroM) / (2.0 * tensionAdmisibleMPa);
            double espesorMm = espesorM * 1000.0;

            return espesorMm;
        }

        // Aplica un espesor mínimo para evitar valores demasiado pequeños.
        public double AplicarEspesorMinimo(double espesorMm, double espesorMinimoMm = 1.5)
        {
            if (espesorMm <= 0)
                return 0;

            return espesorMm < espesorMinimoMm
                ? espesorMinimoMm
                : espesorMm;
        }

        // Calcula la tensión admisible del material.
        public double CalcularTensionAdmisible(double fy, double fu)
        {
            if (fy <= 0 || fu <= 0)
                return 0;

            double tensionAdmisible = Math.Min(0.6 * fy, 0.4 * fu);

            return tensionAdmisible > 0
                ? tensionAdmisible
                : 0;
        }
    }
}