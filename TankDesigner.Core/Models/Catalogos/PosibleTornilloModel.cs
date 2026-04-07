namespace TankDesigner.Core.Models.Catalogos
{
    public class PosibleTornilloModel
    {
        public string Fabricante { get; set; } = string.Empty;

        public string CalidadTornillo { get; set; } = string.Empty;

        public string TipoTornillo
        {
            get => CalidadTornillo;
            set => CalidadTornillo = value;
        }

        public double FyTornillos { get; set; }
        public double FuTornillos { get; set; }

        public double Diametro { get; set; }

        public List<double> Longitud { get; set; } = new List<double>();
        public List<double> Precio { get; set; } = new List<double>();
        public List<double> Peso { get; set; } = new List<double>();

        public string ArandelaSpanish { get; set; } = string.Empty;
        public string ArandelaEnglish { get; set; } = string.Empty;
        public double PrecioArandela { get; set; }

        public string TuercaSpanish { get; set; } = string.Empty;
        public string TuercaEnglish { get; set; } = string.Empty;
        public double PrecioTuerca { get; set; }
    }
}
