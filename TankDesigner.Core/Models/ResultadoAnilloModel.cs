namespace TankDesigner.Core.Models
{
    public class ResultadoAnilloModel
    {
        // Identificación del anillo
        public int NumeroAnillo { get; set; }

        // Geometría del anillo
        public double AlturaInferior { get; set; }
        public double AlturaSuperior { get; set; }
        public double AlturaCentro { get; set; }

        // Presión y espesor principal
        public double Presion { get; set; }
        public double EspesorRequerido { get; set; }
        public double EspesorSeleccionado { get; set; }

        // Estado general del anillo
        public bool EsValido { get; set; }
        public string Mensaje { get; set; } = string.Empty;

        // Datos generales de comprobación
        public double Head { get; set; }
        public double TensionAdmisibleBase { get; set; }
        public double TensionAdmisibleAjustada { get; set; }
        public double CoeficienteNormativa { get; set; }
        public string NormativaAplicada { get; set; } = string.Empty;

        // Esfuerzos principales
        public double HydrostaticHoopLoad { get; set; }

        public double NetTensileStress { get; set; }
        public double AllowableTensileStress { get; set; }

        public double BoltShearStress { get; set; }
        public double AllowableShearStress { get; set; }

        public double HoleBearingStress { get; set; }
        public double AllowableBearingStress { get; set; }

        // Elementos seleccionados
        public string TornilloAplicado { get; set; } = string.Empty;
        public double DiametroTornilloAplicado { get; set; }

        public string ConfiguracionAplicada { get; set; } = string.Empty;
        public int NumeroTornillosVerticales { get; set; }
        public int NumeroTornillosHorizontales { get; set; }
        public int NumeroTornillosHorizontalesCalculo { get; set; }
        public double DiametroAgujero { get; set; }

        // Material de la plancha
        public double FyPlancha { get; set; }
        public double FuPlancha { get; set; }

        // Datos auxiliares del cálculo
        public double PasoS { get; set; }
        public double RelacionR { get; set; }
        public string TipoFallo { get; set; } = string.Empty;
        public string EstadoResumen { get; set; } = string.Empty;
        public bool CumpleTraccion { get; set; }
        public bool CumpleAplastamiento { get; set; }
        public bool CumpleCortante { get; set; }

        // Bloque específico de normativa por anillo
        public ResultadoNormativaAnilloModel ResultadoNormativa { get; set; } = new ResultadoNormativaAnilloModel();

        // Compatibilidad con tu código actual AWWA
        public double AwwaWindFactor { get; set; }
        public double AwwaRoofFactor { get; set; }
        public double AwwaSeismicFactor { get; set; }
        public double AwwaGlobalFactor { get; set; }
        public double AwwaAdditionalLoad { get; set; }

        public double AxialLoad { get; set; }
        public double AxialStress { get; set; }
        public double AllowableAxialStress { get; set; }
        public double AxialHoleBearingStress { get; set; }
        public double AxialAllowableBearingStress { get; set; }
        public double AxialBoltShearStress { get; set; }
        public double AxialAllowableShearStress { get; set; }
        public bool AxialEsValido { get; set; }

        public double WindAlturaEfectiva { get; set; }
        public double WindAxialDeadLoad { get; set; }
        public double WindAxialLoad { get; set; }
        public double WindAxialStress { get; set; }
        public double WindAllowableAxialStress { get; set; }
        public double WindHoleBearingStress { get; set; }
        public double WindAllowableBearingStress { get; set; }
        public double WindBoltShearStress { get; set; }
        public double WindAllowableShearStress { get; set; }
        public bool WindEsValido { get; set; }

        public double SeismicAlturaEfectiva { get; set; }
        public double SeismicAxialDeadLoad { get; set; }
        public double SeismicAxialLoad { get; set; }
        public double SeismicAxialStress { get; set; }
        public double SeismicAllowableAxialStress { get; set; }
        public double SeismicHoleBearingStress { get; set; }
        public double SeismicAllowableBearingStress { get; set; }
        public double SeismicBoltShearStress { get; set; }
        public double SeismicAllowableShearStress { get; set; }
        public bool SeismicEsValido { get; set; }

        public double CombinedTotalHoopLoad { get; set; }
        public double CombinedNetTensileStress { get; set; }
        public double CombinedAllowableTensileStress { get; set; }
        public double CombinedHoleBearingStress { get; set; }
        public double CombinedAllowableBearingStress { get; set; }
        public double CombinedBoltShearStress { get; set; }
        public double CombinedAllowableShearStress { get; set; }
        public bool CombinedEsValido { get; set; }
    }
}