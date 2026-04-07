using System;

namespace TankDesigner.Core.Services
{
    // Servicio con fórmulas de comprobación mecánica del anillo y los tornillos.
    // Contiene cálculos base que luego se ajustan con los coeficientes de cada normativa.
    public class FormulaComprobacionesService
    {
        // Calcula la carga circunferencial debida a la presión hidrostática.
        public double CalcularHydrostaticHoopLoad(double headM, double radioMm)
        {
            if (headM <= 0 || radioMm <= 0)
                return 0;

            return (headM * 9.81) * (radioMm / 1000.0);
        }

        // Calcula la tensión neta de tracción.
        public double CalcularNetTensileStress(
            double hydrostaticHoopLoad,
            double pasoS,
            double espesorMm,
            double diametroAgujeroMm)
        {
            if (hydrostaticHoopLoad <= 0 || pasoS <= 0 || espesorMm <= 0 || diametroAgujeroMm <= 0)
                return 0;

            if (pasoS <= diametroAgujeroMm)
                return 0;

            return (hydrostaticHoopLoad * pasoS) / (espesorMm * (pasoS - diametroAgujeroMm));
        }

        // Calcula la tensión admisible a tracción.
        public double CalcularAllowableTensileStress(
            double fyPlancha,
            double fuPlancha,
            double relacionR,
            double diametroTornilloMm,
            double pasoS)
        {
            if (fyPlancha <= 0 || fuPlancha <= 0 || pasoS <= 0 || diametroTornilloMm <= 0)
                return 0;

            double valor1 = 0.6 * fyPlancha * (1.0 - 0.9 * relacionR + 3.0 * relacionR * diametroTornilloMm / pasoS);
            double valor2 = 0.4 * fuPlancha;

            return Math.Min(valor1, valor2);
        }

        // Calcula el esfuerzo de aplastamiento en la zona del agujero.
        public double CalcularHoleBearingStress(
            double hydrostaticHoopLoad,
            double pasoS,
            double relacionR,
            double diametroTornilloMm,
            double espesorMm)
        {
            if (hydrostaticHoopLoad <= 0 || pasoS <= 0 || relacionR <= 0 || diametroTornilloMm <= 0 || espesorMm <= 0)
                return 0;

            return (hydrostaticHoopLoad * pasoS * relacionR) / (diametroTornilloMm * espesorMm);
        }

        // Calcula el límite admisible de aplastamiento.
        public double CalcularAllowableBearingStress(double fyPlancha)
        {
            if (fyPlancha <= 0)
                return 0;

            return 1.35 * fyPlancha;
        }

        // Calcula el esfuerzo cortante en el tornillo.
        public double CalcularBoltShearStress(
            double hydrostaticHoopLoad,
            double pasoS,
            double relacionR,
            double diametroTornilloMm)
        {
            if (hydrostaticHoopLoad <= 0 || pasoS <= 0 || relacionR <= 0 || diametroTornilloMm <= 0)
                return 0;

            double area = Math.PI * diametroTornilloMm * diametroTornilloMm / 4.0;
            if (area <= 0)
                return 0;

            return (hydrostaticHoopLoad * pasoS * relacionR) / area;
        }

        // Calcula el cortante admisible del tornillo.
        public double CalcularAllowableShearStress(double fuTornillo)
        {
            if (fuTornillo <= 0)
                return 0;

            return 0.25 * fuTornillo;
        }
    }
}