namespace TankDesigner.Web.Services.Ai;

public class AiAnalisisResultadoDto
{
    public string ResumenGeneral { get; set; } = string.Empty;
    public string NivelRiesgo { get; set; } = string.Empty;
    public List<AiHallazgoDto> Hallazgos { get; set; } = new();
}