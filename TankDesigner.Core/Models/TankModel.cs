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

        // Define si el anillo superior es entero, medio o cuarto.
        // Se copia desde Instalacion.TipoMedioAnillo para que el Core pueda generar alturas desde JSON.
        public string TipoAnilloSuperior { get; set; } = "Anillo entero";

        // Modelo real por anillo.
        // Estas listas se rellenan automáticamente desde los JSON del fabricante.
        // No deben usarse como entrada manual principal.
        public List<double> AlturasAnillos { get; set; } = new List<double>();
        public List<string> MaterialesAnillos { get; set; } = new List<string>();
        public List<string> ConfiguracionesAnillos { get; set; } = new List<string>();

        public int NumeroTotalChapas => ChapasPorAnillo * NumeroAnillos;
    }
}
