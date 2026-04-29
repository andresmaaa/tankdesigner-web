namespace TankDesigner.Web.Services.Ai;

public class AiProyectoTecnicoDto
{
    public string NombreProyecto { get; set; } = string.Empty;
    public string Normativa { get; set; } = string.Empty;
    public string Fabricante { get; set; } = string.Empty;

    public double Diametro { get; set; }
    public double AlturaTotal { get; set; }
    public double AlturaPanelBase { get; set; }
    public int NumeroAnillos { get; set; }
    public int ChapasPorAnillo { get; set; }

    public string MaterialPrincipal { get; set; } = string.Empty;
    public string ConfiguracionCalculada { get; set; } = string.Empty;
    public string TornilloCalculado { get; set; } = string.Empty;

    public bool TieneRigidizador { get; set; }
    public string Rigidizador { get; set; } = string.Empty;

    public bool TieneStarterRing { get; set; }

    public double VelocidadViento { get; set; }
    public string ClaseExposicion { get; set; } = string.Empty;

    public double Ss { get; set; }
    public double S1 { get; set; }
    public string SiteClass { get; set; } = string.Empty;

    public string TipoTecho { get; set; } = string.Empty;
    public double CargaMuertaTecho { get; set; }
    public double CargaNieveTecho { get; set; }
    public double CargaVivaTecho { get; set; }

    public bool CalculoValido { get; set; }
    public string MensajeCalculo { get; set; } = string.Empty;

    public List<AiAnilloTecnicoDto> Anillos { get; set; } = new();
    public List<AiHallazgoDto> HallazgosPrevios { get; set; } = new();
}