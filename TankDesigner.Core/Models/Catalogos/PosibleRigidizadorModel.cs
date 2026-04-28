namespace TankDesigner.Core.Models.Catalogos
{
    public class PosibleRigidizadorModel
    {
        public string Fabricante { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public double Altura { get; set; }

        public double Espesor { get; set; }

        public double Peso { get; set; }

        public double Precio { get; set; }
    }
}
