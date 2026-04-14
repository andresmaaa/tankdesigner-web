namespace TankDesigner.Core.Models.Presupuestos
{
    public sealed class ResumenEconomicoPresupuestoModel
    {
        public double TotalMateriales { get; set; }
        public double TotalInstalacion { get; set; }
        public double TotalTransporte { get; set; }
        public double TotalGeneral { get; set; }
        public int NumeroLineasMaterial { get; set; }
        public int NumeroPartidasInstalacion { get; set; }
    }
}
