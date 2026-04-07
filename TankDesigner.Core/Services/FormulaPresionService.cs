namespace TankDesigner.Core.Services
{
    // Servicio con fórmulas básicas de presión hidrostática.
    // Se mantiene común para todas las normativas.
    public class FormulaPresionService
    {
        // Calcula la presión en la base del tanque.
        public double CalcularPresionHidrostaticaBase(double densidadLiquido, double alturaTotalMm)
        {
            if (densidadLiquido <= 0 || alturaTotalMm <= 0)
                return 0;

            double alturaTotalM = alturaTotalMm / 1000.0;

            return 9.81 * densidadLiquido * alturaTotalM;
        }

        // Calcula la presión a una altura concreta del líquido.
        public double CalcularPresionEnAltura(double densidadLiquido, double alturaLiquidoSobrePuntoMm)
        {
            if (densidadLiquido <= 0 || alturaLiquidoSobrePuntoMm <= 0)
                return 0;

            double alturaM = alturaLiquidoSobrePuntoMm / 1000.0;

            return 9.81 * densidadLiquido * alturaM;
        }
    }
}