namespace TankDesigner.Core.Models.Catalogos
{
    public class PosibleConfiguracionModel
    {
        public string Fabricante { get; set; }

        public string Nombre { get; set; }

        public double S { get; set; }

        public double R { get; set; }

        public int NumeroTornillosUnionVertical { get; set; }

        public int NumeroTornillosUnionHorizontal { get; set; }

        public int NumeroTornillosUnionHorizontalCalculo { get; set; }

        public double DiametroAgujero { get; set; }
    }
}
