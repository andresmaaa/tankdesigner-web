namespace TankDesigner.Core.Models
{
    // Modelo de entrada/salida ligera para describir un anillo concreto del tanque.
    // Sirve para pasar alturas, materiales y configuraciones reales por anillo.
    public class AnilloCalculoModel
    {
        public int NumeroAnillo { get; set; }
        public double AlturaMm { get; set; }
        public string Material { get; set; } = string.Empty;
        public string Configuracion { get; set; } = string.Empty;
        public double EspesorMm { get; set; }
    }
}
