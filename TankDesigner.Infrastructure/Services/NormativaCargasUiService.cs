using System.Text;
using TankDesigner.Core.Models;

namespace TankDesigner.Infrastructure.Services
{
    public class NormativaCargasUiService
    {
        public string NormalizarNormativa(string? normativa)
        {
            if (string.IsNullOrWhiteSpace(normativa))
                return "AWWA";

            string n = normativa.Trim().ToUpperInvariant();

            if (n.Contains("AWWA"))
                return "AWWA";

            if (n.Contains("EC") || n.Contains("EUROCODE"))
                return "EC";

            if (n.Contains("ISO"))
                return "ISO";

            return "AWWA";
        }

        public CargasModel CrearCargasProvisionales(string normativa, CargasModel? existente = null)
        {
            string n = NormalizarNormativa(normativa);
            CargasModel cargas = existente ?? new CargasModel();

            cargas.NormativaAplicada = n;

            if (n == "AWWA")
            {
                if (string.IsNullOrWhiteSpace(cargas.RoofType)) cargas.RoofType = "None";
                if (string.IsNullOrWhiteSpace(cargas.RoofAngle)) cargas.RoofAngle = "0°";
                if (string.IsNullOrWhiteSpace(cargas.ClaseExposicion)) cargas.ClaseExposicion = "B";
                if (string.IsNullOrWhiteSpace(cargas.SiteClass)) cargas.SiteClass = "D";
                if (string.IsNullOrWhiteSpace(cargas.SeismicUseGroup)) cargas.SeismicUseGroup = "II";
                if (string.IsNullOrWhiteSpace(cargas.Observaciones)) cargas.Observaciones = "Datos AWWA cargados desde el formulario de cargas.";

                if (cargas.VelocidadViento <= 0) cargas.VelocidadViento = 25.00;
                if (cargas.Ss <= 0) cargas.Ss = 0.40;
                if (cargas.S1 <= 0) cargas.S1 = 0.15;
                if (cargas.TL <= 0) cargas.TL = 8.00;
                if (cargas.DensidadLiquido <= 0) cargas.DensidadLiquido = 1.00;

                if (EsTechoNone(cargas.RoofType))
                {
                    AplicarCargasTechoNone(cargas);
                }
                else
                {
                    if (cargas.RoofDeadLoad <= 0) cargas.RoofDeadLoad = 0.30;
                    if (cargas.RoofSnowLoad <= 0) cargas.RoofSnowLoad = 0.75;
                    if (cargas.RoofLiveLoad <= 0) cargas.RoofLiveLoad = 0.57;
                    if (cargas.RoofCentroid <= 0) cargas.RoofCentroid = 0.00;
                    if (cargas.RoofProjectedArea <= 0) cargas.RoofProjectedArea = 0.00;
                    if (cargas.SnowLoad <= 0) cargas.SnowLoad = cargas.RoofSnowLoad > 0 ? cargas.RoofSnowLoad : 0.75;
                }

                return cargas;
            }

            if (n == "ISO")
            {
                cargas.RoofType = "Provisional ISO";
                cargas.RoofAngle = "ISO-Placeholder";
                cargas.ClaseExposicion = "Terrain II";
                cargas.SiteClass = "Ground C";
                cargas.SeismicUseGroup = "ISO Use Class II";
                cargas.Observaciones = "Datos provisionales ISO cargados automáticamente. Sustituir cuando se implemente la normativa real.";

                cargas.RoofDeadLoad = 0.25;
                cargas.RoofSnowLoad = 0.60;
                cargas.RoofLiveLoad = 0.25;
                cargas.RoofCentroid = 0.00;
                cargas.RoofProjectedArea = 0.00;
                cargas.VelocidadViento = 27.00;
                cargas.SnowLoad = cargas.RoofSnowLoad;
                cargas.Ss = 0.30;
                cargas.S1 = 0.10;
                cargas.TL = 6.00;

                if (cargas.DensidadLiquido <= 0) cargas.DensidadLiquido = 1.00;

                return cargas;
            }

            cargas.RoofType = "Provisional EC";
            cargas.RoofAngle = "EC-Placeholder";
            cargas.ClaseExposicion = "Terrain Category III";
            cargas.SiteClass = "Ground Type C";
            cargas.SeismicUseGroup = "Importance Class II";
            cargas.Observaciones = "Datos provisionales EC cargados automáticamente. Sustituir cuando se implemente la normativa real.";

            cargas.RoofDeadLoad = 0.35;
            cargas.RoofSnowLoad = 0.80;
            cargas.RoofLiveLoad = 0.40;
            cargas.RoofCentroid = 0.00;
            cargas.RoofProjectedArea = 0.00;
            cargas.VelocidadViento = 29.00;
            cargas.SnowLoad = cargas.RoofSnowLoad;
            cargas.Ss = 0.35;
            cargas.S1 = 0.12;
            cargas.TL = 6.00;

            if (cargas.DensidadLiquido <= 0) cargas.DensidadLiquido = 1.00;

            return cargas;
        }

        public void AplicarReglasTecho(CargasModel cargas)
        {
            if (cargas == null)
                return;

            if (EsTechoNone(cargas.RoofType))
                AplicarCargasTechoNone(cargas);
        }

        private static bool EsTechoNone(string? roofType)
        {
            return string.IsNullOrWhiteSpace(roofType)
                   || roofType.Trim().Equals("None", StringComparison.OrdinalIgnoreCase);
        }

        private static void AplicarCargasTechoNone(CargasModel cargas)
        {
            cargas.RoofType = "None";
            cargas.RoofDeadLoad = 0;
            cargas.RoofSnowLoad = 0;
            cargas.RoofLiveLoad = 0;
            cargas.RoofCentroid = 0;
            cargas.RoofProjectedArea = 0;
            cargas.RoofAngle = "0°";
            cargas.SnowLoad = 0;
        }

        public string ConstruirResumenVisual(string normativa)
        {
            string n = NormalizarNormativa(normativa);
            StringBuilder sb = new StringBuilder();

            sb.Append("La normativa activa es \"").Append(n).Append("\".");

            if (n == "AWWA")
            {
                sb.Append(" Se utiliza el formulario completo de AWWA con datos editables de cubierta, viento y sismo.");
            }
            else if (n == "ISO")
            {
                sb.Append(" Se muestran valores provisionales ISO para que el flujo de cálculo e informe no queden vacíos.");
            }
            else
            {
                sb.Append(" Se muestran valores provisionales EC para que el flujo de cálculo e informe no queden vacíos.");
            }

            return sb.ToString();
        }
    }
}