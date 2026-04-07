namespace TankDesigner.Core.Models.Catalogos
{
    public class PosibleStarterRingModel
    {
        public string Fabricante { get; set; }

        public double Altura { get; set; }

        public double DistanciaF { get; set; }

        public int ShearKeysPerLine { get; set; }

        public List<double> F { get; set; } = new List<double>();

        public List<double> MaxShearKeysPerSheet { get; set; } = new List<double>();
    }
}
