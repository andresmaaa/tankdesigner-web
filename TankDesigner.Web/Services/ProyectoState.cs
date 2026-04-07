using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services
{
    public class ProyectoState
    {
        public ProyectoGeneralModel Proyecto { get; set; } = new();
        public TankModel Tanque { get; set; } = new();
        public CargasModel Cargas { get; set; } = new();
        public InstalacionModel Instalacion { get; set; } = new();
        public ResultadoCalculoModel Resultado { get; set; } = new();
        public int? ProyectoIdActual { get; set; }
        public bool TieneProyectoGuardado => ProyectoIdActual.HasValue;


        public void LimpiarTodo()
        {
            ProyectoIdActual = null;
            Proyecto = new ProyectoGeneralModel();
            Tanque = new TankModel();
            Cargas = new CargasModel();
            Instalacion = new InstalacionModel();
            Resultado = new ResultadoCalculoModel();
        }


    }
}