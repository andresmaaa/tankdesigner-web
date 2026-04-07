using TankDesigner.Core.Interfaces;

namespace TankDesigner.Core.Services.Normativas
{
    // Selecciona el servicio de reglas de normativa que corresponde en cada cálculo.
    public class NormativaFormulaSelectorService
    {
        private readonly INormativaFormulaService _awwaService;
        private readonly INormativaFormulaService _isoService;
        private readonly INormativaFormulaService _ecService;

        public NormativaFormulaSelectorService()
        {
            _awwaService = new AwwaFormulaService();
            _isoService = new IsoFormulaService();
            _ecService = new EcFormulaService();
        }

        public INormativaFormulaService ObtenerServicio(string normativa)
        {
            if (string.IsNullOrWhiteSpace(normativa))
                return _isoService;

            string valor = normativa.Trim().ToUpperInvariant();

            if (valor.Contains("AWWA"))
                return _awwaService;

            if (valor.Contains("EC") || valor.Contains("EUROCODE"))
                return _ecService;

            if (valor.Contains("ISO"))
                return _isoService;

            return _isoService;
        }
    }
}