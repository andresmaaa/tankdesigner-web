using System.Globalization;
using System.Linq;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Presupuestos;
using TankDesigner.Core.Services;

namespace TankDesigner.Web.Services
{
    public class CalculoWebService
    {
        private readonly MotorCalculoService _motorCalculoService;
        private readonly CalculoInputAdapterService _inputAdapterService;
        private readonly CalculoGeometriaService _calculoGeometriaService;
        private readonly PresupuestoInstalacionExcelService _presupuestoInstalacionExcelService;
        private readonly CalculoVigasTechoConicoService _calculoVigasTechoConicoService;

        public CalculoWebService()
        {
            _motorCalculoService = new MotorCalculoService();
            _inputAdapterService = new CalculoInputAdapterService();
            _calculoGeometriaService = new CalculoGeometriaService();
            _presupuestoInstalacionExcelService = new PresupuestoInstalacionExcelService();
            _calculoVigasTechoConicoService = new CalculoVigasTechoConicoService();
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
            NormalizarTanque(tanque, proyecto);
            NormalizarCargas(proyecto, tanque, cargas);
            RecalcularGeometria(proyecto, tanque);

            var input = _inputAdapterService.Construir(proyecto, tanque, cargas);

            if (input == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No se pudo construir la entrada de cálculo.",
                    VigasTechoConico = _calculoVigasTechoConicoService.Calcular(tanque, cargas, null)
                };
            }

            if (input.NumeroAnillos <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene un número de anillos válido.",
                    VigasTechoConico = _calculoVigasTechoConicoService.Calcular(tanque, cargas, null)
                };
            }

            if (input.ChapasPorAnillo <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene un número de chapas por anillo válido.",
                    VigasTechoConico = _calculoVigasTechoConicoService.Calcular(tanque, cargas, null)
                };
            }

            if (input.Diametro <= 0 || input.AlturaTotal <= 0 || input.AlturaPanelBase <= 0)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "La entrada de cálculo no tiene dimensiones geométricas válidas.",
                    VigasTechoConico = _calculoVigasTechoConicoService.Calcular(tanque, cargas, null)
                };
            }

            var resultado = _motorCalculoService.Calcular(input) ?? new ResultadoCalculoModel();

            HidratarResultadoDesdeInput(resultado, input);

            if (resultado.Anillos == null)
                resultado.Anillos = new List<ResultadoAnilloModel>();

            resultado.PresupuestoInstalacion = CalcularPresupuestoInstalacion(proyecto, tanque, instalacion, resultado);

            resultado.VigasTechoConico = _calculoVigasTechoConicoService.Calcular(
                tanque,
                cargas,
                resultado);

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
            proyecto.MaterialPrincipal = string.IsNullOrWhiteSpace(proyecto.MaterialPrincipal)
                ? "S235"
                : proyecto.MaterialPrincipal.Trim();

            proyecto.NombreProyecto = (proyecto.NombreProyecto ?? string.Empty).Trim();
            proyecto.ClienteReferencia = (proyecto.ClienteReferencia ?? string.Empty).Trim();
        }

        private static void NormalizarTanque(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque.ChapasPorAnillo <= 0)
                tanque.ChapasPorAnillo = 16;

            if (tanque.NumeroAnillos <= 0)
                tanque.NumeroAnillos = 6;

            if (tanque.AnilloArranque <= 0)
                tanque.AnilloArranque = 1;

            if (tanque.AnilloArranque > tanque.NumeroAnillos)
                tanque.AnilloArranque = tanque.NumeroAnillos;

            if (tanque.BordeLibre < 0)
                tanque.BordeLibre = 0;
            else if (tanque.BordeLibre == 0)
                tanque.BordeLibre = 300;

            if (tanque.DensidadLiquido < 0)
                tanque.DensidadLiquido = 0;
            else if (tanque.DensidadLiquido == 0)
                tanque.DensidadLiquido = 1;

            tanque.Modelo = (tanque.Modelo ?? string.Empty).Trim();

            tanque.AlturasAnillos ??= new List<double>();
            tanque.MaterialesAnillos ??= new List<string>();
            tanque.ConfiguracionesAnillos ??= new List<string>();

            tanque.MaterialesAnillos = tanque.MaterialesAnillos
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            tanque.ConfiguracionesAnillos = tanque.ConfiguracionesAnillos
                .Where(x => x != null)
                .Select(x => x.Trim())
                .ToList();

            tanque.AlturasAnillos = tanque.AlturasAnillos
                .Where(x => x > 0)
                .ToList();
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

            bool techoNone = string.IsNullOrWhiteSpace(cargas.RoofType)
                             || cargas.RoofType.Equals("None", StringComparison.OrdinalIgnoreCase);

            if (techoNone)
            {
                cargas.RoofType = "None";
                cargas.RoofDeadLoad = 0;
                cargas.RoofSnowLoad = 0;
                cargas.RoofLiveLoad = 0;
                cargas.RoofCentroid = 0;
                cargas.RoofProjectedArea = 0;
                cargas.RoofAngle = "0°";
                cargas.SnowLoad = 0;
            }

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
        }

        private PresupuestoInstalacionResultadoModel? CalcularPresupuestoInstalacion(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            InstalacionModel instalacion,
            ResultadoCalculoModel resultado)
        {
            try
            {
                if (resultado == null || resultado.Anillos == null || resultado.Anillos.Count == 0)
                    return null;

                var espesores = resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(x => x.EspesorSeleccionado > 0
                        ? Convert.ToDecimal(x.EspesorSeleccionado, CultureInfo.InvariantCulture)
                        : Convert.ToDecimal(x.EspesorRequerido, CultureInfo.InvariantCulture))
                    .Where(x => x > 0)
                    .ToList();

                if (espesores.Count == 0)
                    return null;

                var input = new PresupuestoInstalacionInputModel
                {
                    Fabricante = MapearFabricante(proyecto.Fabricante),
                    NumeroPlacasPorAnillo = resultado.ChapasPorAnillo > 0 ? resultado.ChapasPorAnillo : tanque.ChapasPorAnillo,
                    NumeroAnillos = resultado.NumeroAnillos > 0 ? resultado.NumeroAnillos : tanque.NumeroAnillos,
                    TieneStarterRing = instalacion.StarterRing || resultado.TieneStarterRing,
                    TipoTecho = MapearTipoTecho(instalacion.TipoTecho),
                    TipoEscalera = MapearTipoEscalera(instalacion.TipoEscalera),
                    NumeroEscaleras = Math.Max(0, instalacion.NumeroEscaleras),

                    ConexionesDn25a150 = Math.Max(0, instalacion.ConexionesDN25_DN150),
                    ConexionesDn150a300 = Math.Max(0, instalacion.ConexionesDN150_DN300),
                    ConexionesDn300a500 = Math.Max(0, instalacion.ConexionesDN300_DN500),
                    ConexionesMayor500 = Math.Max(0, instalacion.ConexionesMayorDN500),
                    NumeroBocasHombre = Math.Max(1, instalacion.NumeroBocasHombre),
                    NumeroLineasRigidizador = Math.Max(0, resultado.TieneRigidizadorBase ? 1 : 0),

                    TamanoCuadrilla = Math.Max(1, instalacion.TamanoCuadrilla),
                    HorasTrabajoPorDia = Convert.ToDecimal(
                        instalacion.HorasTrabajoDia > 0 ? instalacion.HorasTrabajoDia : 8,
                        CultureInfo.InvariantCulture),

                    PorcentajeLluvia = Convert.ToDecimal(
                        instalacion.DiasLluviaPorcentaje > 1
                            ? instalacion.DiasLluviaPorcentaje / 100.0
                            : instalacion.DiasLluviaPorcentaje,
                        CultureInfo.InvariantCulture),

                    NumeroSiteManagers = Math.Max(0, instalacion.SiteManager),
                    NumeroTecnicosSeguridad = Math.Max(0, instalacion.TecnicoSeguridad),
                    UbicacionObra = MapearUbicacion(instalacion.LugarObra),

                    DiametroMetros = Convert.ToDecimal(
                        (resultado.Diametro > 0 ? resultado.Diametro : tanque.Diametro) / 1000.0,
                        CultureInfo.InvariantCulture),

                    EspesoresAnillosMm = espesores,
                    DistanciaAlojamientoObraHoras = Convert.ToDecimal(instalacion.DistanciaAlojamientoObra, CultureInfo.InvariantCulture),
                    CosteTransporteManual = Convert.ToDecimal(instalacion.CosteTransporteManual, CultureInfo.InvariantCulture)
                };

                if (input.NumeroAnillos <= 0 || input.NumeroPlacasPorAnillo <= 0 || input.DiametroMetros <= 0)
                    return null;

                if (input.EspesoresAnillosMm.Count != input.NumeroAnillos)
                    return null;

                return _presupuestoInstalacionExcelService.Calcular(input);
            }
            catch
            {
                return null;
            }
        }

        private static FabricantePresupuesto MapearFabricante(string? fabricante)
        {
            var clave = NormalizarClave(fabricante);

            if (clave.Contains("balmoral"))
                return FabricantePresupuesto.Balmoral;

            if (clave.Contains("permastore"))
                return FabricantePresupuesto.Permastore;

            return FabricantePresupuesto.DL2;
        }

        private static TipoTechoPresupuesto MapearTipoTecho(string? tipoTecho)
        {
            var clave = NormalizarClave(tipoTecho);

            if (clave.Contains("conico"))
                return TipoTechoPresupuesto.Conico;

            if (clave.Contains("plano"))
                return TipoTechoPresupuesto.Plano;

            if (clave.Contains("domo"))
                return TipoTechoPresupuesto.DomoGeodesico;

            return TipoTechoPresupuesto.SinTecho;
        }

        private static TipoEscaleraPresupuesto MapearTipoEscalera(string? tipoEscalera)
        {
            var clave = NormalizarClave(tipoEscalera);

            if (clave.Contains("vertical"))
                return TipoEscaleraPresupuesto.Vertical;

            if (clave.Contains("helicoidal"))
                return TipoEscaleraPresupuesto.Helicoidal;

            return TipoEscaleraPresupuesto.SinEscalera;
        }

        private static UbicacionObraPresupuesto MapearUbicacion(string? ubicacion)
        {
            var clave = NormalizarClave(ubicacion);

            if (clave.Contains("internacional"))
                return UbicacionObraPresupuesto.Internacional;

            if (clave.Contains("europa"))
                return UbicacionObraPresupuesto.Europa;

            return UbicacionObraPresupuesto.Nacional;
        }

        private static string NormalizarClave(string? valor)
        {
            return (valor ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace("á", "a")
                .Replace("é", "e")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("ú", "u")
                .Replace("ü", "u");
        }
    }
}