namespace TankDesigner.Core.Models
{
    // Modelo auxiliar para guardar resultados propios de cada normativa.
    // Permite ampliar ISO y EC sin ensuciar el modelo general.
    public class ResultadoNormativaModel
    {
        public string Normativa { get; set; } = string.Empty;

        // Valores genéricos por normativa
        public double CoeficienteGlobal { get; set; }
        public double CoeficienteViento { get; set; }
        public double CoeficienteSismo { get; set; }
        public double CoeficienteCubierta { get; set; }

        // Observaciones o mensajes técnicos específicos
        public string Observaciones { get; set; } = string.Empty;
    }
}