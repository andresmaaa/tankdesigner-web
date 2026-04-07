using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services;

public class InformeRequest
{
    public ProyectoGeneralModel Proyecto { get; set; } = new();
    public TankModel Tanque { get; set; } = new();
    public CargasModel Cargas { get; set; } = new();
    public InstalacionModel Instalacion { get; set; } = new();
    public ResultadoCalculoModel Resultado { get; set; } = new();
}   