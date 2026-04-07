namespace TankDesigner.Core.Models
{
    public class CalculoTanqueInputModel
    {
        // Datos generales del proyecto
        public string Fabricante { get; set; } = string.Empty;
        public string Normativa { get; set; } = string.Empty;
        public string MaterialPrincipal { get; set; } = string.Empty;

        // Geometría básica del tanque
        public int ChapasPorAnillo { get; set; }
        public int NumeroAnillos { get; set; }
        public int AnilloArranque { get; set; }

        public double BordeLibre { get; set; }
        public double DensidadLiquido { get; set; }

        public double Diametro { get; set; }
        public double AlturaTotal { get; set; }
        public double AlturaPanelBase { get; set; }

        public string Modelo { get; set; } = string.Empty;

        // Datos de cargas comunes
        public string NormativaAplicadaCargas { get; set; } = string.Empty;

        public double VelocidadViento { get; set; }
        public double SnowLoad { get; set; }

        public string RoofType { get; set; } = string.Empty;
        public double RoofDeadLoad { get; set; }
        public double RoofSnowLoad { get; set; }
        public double RoofLiveLoad { get; set; }
        public double RoofCentroid { get; set; }
        public double RoofProjectedArea { get; set; }
        public string RoofAngle { get; set; } = string.Empty;
        public string ClaseExposicion { get; set; } = string.Empty;

        public double Ss { get; set; }
        public double S1 { get; set; }
        public double TL { get; set; }
        public string SiteClass { get; set; } = string.Empty;
        public string SeismicUseGroup { get; set; } = string.Empty;

        public string ObservacionesCargas { get; set; } = string.Empty;

        // Bloque específico de normativa
        public ParametrosNormativaModel ParametrosNormativa { get; set; } = new ParametrosNormativaModel();

        // Compatibilidad con tu código actual AWWA
        public double AwwaWindFactor { get; set; }
        public double AwwaRoofFactor { get; set; }
        public double AwwaSeismicFactor { get; set; }
        public double AwwaGlobalFactor { get; set; }
    }
}