using TankDesigner.Core.Interfaces;

namespace TankDesigner.Core.Services.Normativas
{
    // Servicio que selecciona qué cálculo usar según la normativa.
    public class NormativaCalculoSelectorService
    {
        private readonly INormativaCalculoService _awwaService;
        private readonly INormativaCalculoService _isoService;
        private readonly INormativaCalculoService _ecService;

        public NormativaCalculoSelectorService()
        {
            _awwaService = new AwwaCalculoService();
            _isoService = new IsoCalculoService();
            _ecService = new EcCalculoService();
        }

        // Devuelve el servicio adecuado según la normativa indicada.
        public INormativaCalculoService ObtenerServicio(string normativa)
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

            // Si no reconoce la normativa, usa ISO por defecto.
            return _isoService;
        }
    }
}