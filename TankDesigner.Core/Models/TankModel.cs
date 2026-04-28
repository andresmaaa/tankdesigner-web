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

        // Modelo real por anillo.
        // Si estas listas vienen vacías, el sistema usa los valores globales como respaldo.
        public List<double> AlturasAnillos { get; set; } = new List<double>();
        public List<string> MaterialesAnillos { get; set; } = new List<string>();
        public List<string> ConfiguracionesAnillos { get; set; } = new List<string>();

        public int NumeroTotalChapas => ChapasPorAnillo * NumeroAnillos;
    }
}
