namespace TankDesigner.Web.Services.Ai;

public class AiHallazgoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Campo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Recomendacion { get; set; } = string.Empty;
    public int Prioridad { get; set; }
}