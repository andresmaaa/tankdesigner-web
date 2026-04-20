namespace TankDesigner.Core.Models
{
    public class CargasModel
    {
        // Compatibilidad con el resto del proyecto actual
        public double DensidadLiquido { get; set; }
        public double VelocidadViento { get; set; }
        public double SnowLoad { get; set; }

        // Control de normativa (NO se asigna valor por defecto)
        public string NormativaAplicada { get; set; }

        // AWWA - Techo
        public string RoofAngle { get; set; } = "0°";

        public string RoofType { get; set; }
        public double RoofDeadLoad { get; set; }
        public double RoofSnowLoad { get; set; }
        public double RoofLiveLoad { get; set; }
        public double RoofCentroid { get; set; }
        public double RoofProjectedArea { get; set; }
        public string AnguloSuperior { get; set; } = "";

        // AWWA - Viento
        public string ClaseExposicion { get; set; }

        // AWWA - Sismo
        public double Ss { get; set; }
        public double S1 { get; set; }
        public double TL { get; set; }
        public string SiteClass { get; set; }
        public string SeismicUseGroup { get; set; }

        public string Observaciones { get; set; }
    }
}