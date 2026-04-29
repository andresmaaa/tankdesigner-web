namespace TankDesigner.Web.Services.Ai;

public class AiAnilloTecnicoDto
{
    public int NumeroAnillo { get; set; }

    public double AlturaInferior { get; set; }
    public double AlturaSuperior { get; set; }

    public string Material { get; set; } = string.Empty;
    public string Configuracion { get; set; } = string.Empty;
    public string Tornillo { get; set; } = string.Empty;

    public double Presion { get; set; }
    public double EspesorRequerido { get; set; }
    public double EspesorSeleccionado { get; set; }

    public double AprovechamientoEspesor { get; set; }

    public double TensionNeta { get; set; }
    public double TensionAdmisible { get; set; }
    public double AprovechamientoTraccion { get; set; }

    public double CortanteTornillo { get; set; }
    public double CortanteAdmisible { get; set; }
    public double AprovechamientoCortante { get; set; }

    public double Aplastamiento { get; set; }
    public double AplastamientoAdmisible { get; set; }
    public double AprovechamientoAplastamiento { get; set; }

    public bool CumpleTraccion { get; set; }
    public bool CumpleCortante { get; set; }
    public bool CumpleAplastamiento { get; set; }

    public bool CumpleAxial { get; set; }
    public bool CumpleViento { get; set; }
    public bool CumpleSismo { get; set; }
    public bool CumpleCombinado { get; set; }

    public string EstadoResumen { get; set; } = string.Empty;
    public string TipoFallo { get; set; } = string.Empty;
}