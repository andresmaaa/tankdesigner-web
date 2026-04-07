namespace TankDesigner.Core.Models
{
    public class ResultadoCalculoModel
    {
        // Resultado general del cálculo
        public List<ResultadoAnilloModel> Anillos { get; set; } = new List<ResultadoAnilloModel>();
        public bool EsValido { get; set; }
        public string Mensaje { get; set; } = string.Empty;

        // Datos generales del proyecto calculado
        public string Normativa { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;
        public string MaterialPrincipal { get; set; } = string.Empty;

        // Geometría principal del tanque
        public double Diametro { get; set; }
        public double AlturaTotal { get; set; }
        public double AlturaPanelBase { get; set; }

        public int ChapasPorAnillo { get; set; }
        public int NumeroAnillos { get; set; }
        public int AnilloArranque { get; set; }

        public double BordeLibre { get; set; }
        public double DensidadLiquido { get; set; }

        // Resultado básico común a cualquier normativa
        public double PresionHidrostaticaBase { get; set; }

        // Configuración seleccionada
        public bool TieneConfiguracion { get; set; }
        public string NombreConfiguracion { get; set; } = string.Empty;
        public int NumeroTornillosVerticales { get; set; }
        public int NumeroTornillosHorizontales { get; set; }
        public int NumeroTornillosHorizontalesCalculo { get; set; }
        public double DiametroAgujero { get; set; }

        // Tornillo base seleccionado
        public bool TieneTornilloBase { get; set; }
        public string NombreTornilloBase { get; set; } = string.Empty;
        public double DiametroTornilloBase { get; set; }

        // Rigidizador base seleccionado
        public bool TieneRigidizadorBase { get; set; }
        public string NombreRigidizadorBase { get; set; } = string.Empty;
        public double AlturaRigidizadorBase { get; set; }
        public double EspesorRigidizadorBase { get; set; }
        public double PesoRigidizadorBase { get; set; }
        public double PrecioRigidizadorBase { get; set; }

        // Starter ring seleccionado
        public bool TieneStarterRing { get; set; }
        public double AlturaStarterRing { get; set; }
        public double DistanciaFStarterRing { get; set; }
        public int ShearKeysPorLineaStarterRing { get; set; }
        public string FStarterRingTexto { get; set; } = string.Empty;
        public string MaxShearKeysPorPlanchaTexto { get; set; } = string.Empty;

        // Selección real obtenida a partir del cálculo de anillos
        public string NombreConfiguracionCalculada { get; set; } = string.Empty;
        public string NombreTornilloCalculado { get; set; } = string.Empty;
        public double DiametroTornilloCalculado { get; set; }
        public double DiametroAgujeroCalculado { get; set; }
        public bool TieneSeleccionRealCalculada { get; set; }

        // Datos adicionales de instalación o cimentación
        public double EmbebidoCalculado { get; set; }

        // Resultados específicos por normativa
        public ResultadoNormativaModel ResultadoNormativa { get; set; } = new ResultadoNormativaModel();

        // Compatibilidad con tu código actual AWWA
        public double WindShearForceAtBase { get; set; }
        public double WindShear { get; set; }
        public double WindOverturningMoment { get; set; }
        public double MaximumAxialLoadDueToWindOTM { get; set; }
        public double RoofWindUplift { get; set; }

        public double WeightOfContents { get; set; }
        public double TankShellDeadLoad { get; set; }
        public double RoofDeadLoad { get; set; }
        public double SeismicShearA { get; set; }
        public double SeismicShearB { get; set; }
        public double SeismicOTMAtBaseOfShell { get; set; }
        public double MaximumAxialLoadDueToSeismicOTMatBaseOfShell { get; set; }
        public double SeismicOTMatTopOfFoundation { get; set; }
        public double CombinedHydrostaticHydrodynamicShear { get; set; }
        public double CombinedHydrostaticHydrodynamicMoment { get; set; }
        public double SloshingWave { get; set; }
        public double MinimumFreeboardRequirements { get; set; }
        public bool FreeboardIsOk { get; set; }

        public double AwwaAi { get; set; }
        public double AwwaAc { get; set; }
        public double AwwaAv { get; set; }
    }
}