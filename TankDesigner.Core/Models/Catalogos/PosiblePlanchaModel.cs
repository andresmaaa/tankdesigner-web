namespace TankDesigner.Core.Models.Catalogos
{
    public class PosiblePlanchaModel
    {
        public string Fabricante { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        public string Acabado { get; set; } = string.Empty;

        public double Fy { get; set; }
        public double Fu { get; set; }

        public double Altura { get; set; }
        public double Ancho { get; set; }

        public List<double> Espesor { get; set; } = new List<double>();
        public List<double> Precio { get; set; } = new List<double>();
    }
}
