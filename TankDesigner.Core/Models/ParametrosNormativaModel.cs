namespace TankDesigner.Core.Models
{
    // Modelo para guardar parámetros adicionales según la normativa.
    // Así luego se pueden añadir campos sin tocar el input general.
    public class ParametrosNormativaModel
    {
        public string Normativa { get; set; } = string.Empty;

        public double CoeficienteViento { get; set; }
        public double CoeficienteCubierta { get; set; }
        public double CoeficienteSismo { get; set; }
        public double CoeficienteGlobal { get; set; }

        public string Observaciones { get; set; } = string.Empty;
    }
}