using TankDesigner.Core.Models;
using TankDesigner.Core.Services;

namespace TankDesigner.Web.Services
{
    public class CalculoWebService
    {
        private readonly MotorCalculoService _motorCalculoService;
        private readonly CalculoInputAdapterService _inputAdapterService;
        private readonly CalculoGeometriaService _calculoGeometriaService;

        public CalculoWebService()
        {
            _motorCalculoService = new MotorCalculoService();
            _inputAdapterService = new CalculoInputAdapterService();
            _calculoGeometriaService = new CalculoGeometriaService();
        }

        public ResultadoCalculoModel Calcular(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas,
            InstalacionModel instalacion)
        {
            proyecto ??= new ProyectoGeneralModel();
            tanque ??= new TankModel();
            cargas ??= new CargasModel();
            instalacion ??= new InstalacionModel();

            NormalizarProyecto(proyecto);
            NormalizarTanque(tanque);
            NormalizarCargas(proyecto, tanque, cargas);
            RecalcularGeometria(proyecto, tanque);

            var input = _inputAdapterService.Construir(proyecto, tanque, cargas);

            if (input == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No se pudo construir la entrada de cálculo."
                };
            }

            if (input.NumeroAnillos <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene un número de anillos válido."
                };
            }

            if (input.ChapasPorAnillo <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene un número de chapas por anillo válido."
                };
            }

            if (input.Diametro <= 0 || input.AlturaTotal <= 0 || input.AlturaPanelBase <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene dimensiones geométricas válidas."
                };
            }

            var resultado = _motorCalculoService.Calcular(input) ?? new ResultadoCalculoModel();

            HidratarResultadoDesdeInput(resultado, input);

            if (resultado.Anillos == null)
                resultado.Anillos = new List<ResultadoAnilloModel>();

            if (string.IsNullOrWhiteSpace(resultado.Mensaje))
            {
                resultado.Mensaje = resultado.Anillos.Count > 0
                    ? "Cálculo realizado correctamente."
                    : "Cálculo realizado, pero no se han generado anillos de resultado.";
            }

            return resultado;
        }

        private static void NormalizarProyecto(ProyectoGeneralModel proyecto)
        {
            proyecto.IdiomaInforme = string.IsNullOrWhiteSpace(proyecto.IdiomaInforme)
                ? "ES"
                : proyecto.IdiomaInforme.Trim().ToUpperInvariant();

            proyecto.ModeloCalculo = string.IsNullOrWhiteSpace(proyecto.ModeloCalculo)
                ? "Simple"
                : proyecto.ModeloCalculo.Trim();

            proyecto.Normativa = (proyecto.Normativa ?? string.Empty).Trim();
            proyecto.Fabricante = (proyecto.Fabricante ?? string.Empty).Trim();
            proyecto.MaterialPrincipal = (proyecto.MaterialPrincipal ?? string.Empty).Trim();
            proyecto.NombreProyecto = (proyecto.NombreProyecto ?? string.Empty).Trim();
            proyecto.ClienteReferencia = (proyecto.ClienteReferencia ?? string.Empty).Trim();
        }

        private static void NormalizarTanque(TankModel tanque)
        {
            if (tanque.ChapasPorAnillo <= 0)
                tanque.ChapasPorAnillo = 16; // igual que WPF (SelectedIndex=0)

            if (tanque.NumeroAnillos <= 0)
                tanque.NumeroAnillos = 6; // igual que WPF (SelectedIndex=0)

            if (tanque.AnilloArranque <= 0)
                tanque.AnilloArranque = 1; // igual que WPF (SelectedIndex=0)

            if (tanque.AnilloArranque > tanque.NumeroAnillos)
                tanque.AnilloArranque = tanque.NumeroAnillos;

            if (tanque.BordeLibre < 0)
                tanque.BordeLibre = 0;
            else if (tanque.BordeLibre == 0)
                tanque.BordeLibre = 300; // igual que WPF

            if (tanque.DensidadLiquido < 0)
                tanque.DensidadLiquido = 0;
            else if (tanque.DensidadLiquido == 0)
                tanque.DensidadLiquido = 1; // igual que WPF

            tanque.Modelo = (tanque.Modelo ?? string.Empty).Trim();
        }

        private static void NormalizarCargas(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas)
        {
            cargas.NormativaAplicada = string.IsNullOrWhiteSpace(cargas.NormativaAplicada)
                ? (proyecto.Normativa ?? string.Empty).Trim()
                : cargas.NormativaAplicada.Trim();

            cargas.RoofType = (cargas.RoofType ?? string.Empty).Trim();
            cargas.RoofAngle = (cargas.RoofAngle ?? string.Empty).Trim();
            cargas.ClaseExposicion = (cargas.ClaseExposicion ?? string.Empty).Trim();
            cargas.SiteClass = (cargas.SiteClass ?? string.Empty).Trim();
            cargas.SeismicUseGroup = (cargas.SeismicUseGroup ?? string.Empty).Trim();
            cargas.Observaciones = (cargas.Observaciones ?? string.Empty).Trim();

            if (cargas.DensidadLiquido <= 0 && tanque.DensidadLiquido > 0)
                cargas.DensidadLiquido = tanque.DensidadLiquido;
        }

        private void RecalcularGeometria(ProyectoGeneralModel proyecto, TankModel tanque)
        {
            tanque.AlturaPanelBase = _calculoGeometriaService.ObtenerAlturaPanelBase(tanque, proyecto);
            tanque.AlturaTotal = _calculoGeometriaService.ObtenerAlturaTotal(tanque, proyecto);
            tanque.Diametro = _calculoGeometriaService.ObtenerDiametro(tanque, proyecto);
        }

        private static void HidratarResultadoDesdeInput(
            ResultadoCalculoModel resultado,
            CalculoTanqueInputModel input)
        {
            resultado.Normativa = string.IsNullOrWhiteSpace(resultado.Normativa)
                ? input.Normativa
                : resultado.Normativa;

            resultado.Fabricante = string.IsNullOrWhiteSpace(resultado.Fabricante)
                ? input.Fabricante
                : resultado.Fabricante;

            resultado.MaterialPrincipal = string.IsNullOrWhiteSpace(resultado.MaterialPrincipal)
                ? input.MaterialPrincipal
                : resultado.MaterialPrincipal;

            if (resultado.Diametro <= 0)
                resultado.Diametro = input.Diametro;

            if (resultado.AlturaTotal <= 0)
                resultado.AlturaTotal = input.AlturaTotal;

            if (resultado.AlturaPanelBase <= 0)
                resultado.AlturaPanelBase = input.AlturaPanelBase;

            if (resultado.ChapasPorAnillo <= 0)
                resultado.ChapasPorAnillo = input.ChapasPorAnillo;

            if (resultado.NumeroAnillos <= 0)
                resultado.NumeroAnillos = input.NumeroAnillos;

            if (resultado.AnilloArranque <= 0)
                resultado.AnilloArranque = input.AnilloArranque;

            if (resultado.BordeLibre <= 0)
                resultado.BordeLibre = input.BordeLibre;

            if (resultado.DensidadLiquido <= 0)
                resultado.DensidadLiquido = input.DensidadLiquido;
        }
    }
}