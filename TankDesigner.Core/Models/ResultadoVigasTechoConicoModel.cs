namespace TankDesigner.Core.Models;

public class ResultadoVigasTechoConicoModel
{
    public bool Aplica { get; set; }
    public string TipoTecho { get; set; } = string.Empty;

    public int NumeroVigas { get; set; }
    public double AnguloTechoGrados { get; set; }

    public double DiametroTanque { get; set; }
    public double RadioTanque { get; set; }

    public double AlturaCono { get; set; }
    public double LongitudViga { get; set; }

    public double SeparacionPerimetral { get; set; }
    public double CargaSuperficialTotal { get; set; }
    public double CargaPorViga { get; set; }

    public string PerfilSugerido { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}