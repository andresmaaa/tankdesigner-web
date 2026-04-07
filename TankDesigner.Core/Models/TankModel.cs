namespace TankDesigner.Core.Models
{
    public class TankModel
    {
        public int ChapasPorAnillo { get; set; }
        public int NumeroAnillos { get; set; }
        public int AnilloArranque { get; set; }
        public double BordeLibre { get; set; }
        public double DensidadLiquido { get; set; }
        public string Modelo { get; set; } = string.Empty;
        public double Diametro { get; set; }
        public double AlturaTotal { get; set; }
        public double AlturaPanelBase { get; set; }
        public int NumeroTotalChapas => ChapasPorAnillo * NumeroAnillos;
    }
}
