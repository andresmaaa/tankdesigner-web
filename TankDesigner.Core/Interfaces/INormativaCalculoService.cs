using TankDesigner.Core.Models;

namespace TankDesigner.Core.Interfaces
{
    // Contrato base para cualquier servicio de cálculo por normativa.
    // Cada normativa debe implementar este método y devolver un resultado completo.
    public interface INormativaCalculoService
    {
        ResultadoCalculoModel Calcular(CalculoTanqueInputModel input);
    }
}