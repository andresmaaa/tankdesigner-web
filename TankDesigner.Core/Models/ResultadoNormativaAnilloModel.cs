namespace TankDesigner.Core.Models
{
    // Modelo para guardar resultados específicos de normativa a nivel de anillo.
    public class ResultadoNormativaAnilloModel
    {
        public string Normativa { get; set; } = string.Empty;

        public double CoeficienteViento { get; set; }
        public double CoeficienteCubierta { get; set; }
        public double CoeficienteSismo { get; set; }
        public double CoeficienteGlobal { get; set; }

        public double CargaAdicional { get; set; }

        public string Observaciones { get; set; } = string.Empty;
    }
}