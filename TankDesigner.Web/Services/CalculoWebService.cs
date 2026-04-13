using System.Globalization;
using System.Linq;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Presupuestos;
using TankDesigner.Core.Services;

namespace TankDesigner.Web.Services
{
    // Servicio principal que orquesta todo el cálculo en la web
    // Actúa como puente entre UI (Blazor) y el motor de cálculo real
    public class CalculoWebService
    {
        // Servicios internos de cálculo
        private readonly MotorCalculoService _motorCalculoService;
        private readonly CalculoInputAdapterService _inputAdapterService;
        private readonly CalculoGeometriaService _calculoGeometriaService;
        private readonly PresupuestoInstalacionExcelService _presupuestoInstalacionExcelService;

        public CalculoWebService()
        {
            // Se inicializan manualmente (no usando DI aquí)
            _motorCalculoService = new MotorCalculoService();
            _inputAdapterService = new CalculoInputAdapterService();
            _calculoGeometriaService = new CalculoGeometriaService();
            _presupuestoInstalacionExcelService = new PresupuestoInstalacionExcelService();
        }

        // Método principal que ejecuta todo el flujo de cálculo
        public ResultadoCalculoModel Calcular(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas,
            InstalacionModel instalacion)
        {
            // Evita nulls creando objetos vacíos si vienen null
            proyecto ??= new ProyectoGeneralModel();
            tanque ??= new TankModel();
            cargas ??= new CargasModel();
            instalacion ??= new InstalacionModel();

            // Normaliza todos los datos de entrada
            NormalizarProyecto(proyecto);
            NormalizarTanque(tanque);
            NormalizarCargas(proyecto, tanque, cargas);

            // Recalcula geometría antes de calcular
            RecalcularGeometria(proyecto, tanque);

            // Construye el input real del motor de cálculo
            var input = _inputAdapterService.Construir(proyecto, tanque, cargas);

            // Validaciones básicas del input
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

            // Ejecuta el motor de cálculo principal
            var resultado = _motorCalculoService.Calcular(input) ?? new ResultadoCalculoModel();

            // Completa datos que puedan faltar en el resultado
            HidratarResultadoDesdeInput(resultado, input);

            // Asegura que la lista de anillos existe
            if (resultado.Anillos == null)
                resultado.Anillos = new List<ResultadoAnilloModel>();

            // Calcula el presupuesto de instalación
            resultado.PresupuestoInstalacion = CalcularPresupuestoInstalacion(proyecto, tanque, instalacion, resultado);

            // Mensaje final del cálculo
            if (string.IsNullOrWhiteSpace(resultado.Mensaje))
            {
                resultado.Mensaje = resultado.Anillos.Count > 0
                    ? "Cálculo realizado correctamente."
                    : "Cálculo realizado, pero no se han generado anillos de resultado.";
            }

            return resultado;
        }

        // Normaliza datos del proyecto (strings, idioma, etc.)
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

        // Normaliza datos del tanque y aplica valores por defecto
        private static void NormalizarTanque(TankModel tanque)
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
        }

        // Normaliza cargas y sincroniza con el tanque si hace falta
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

            // Si no hay densidad en cargas, usa la del tanque
            if (cargas.DensidadLiquido <= 0 && tanque.DensidadLiquido > 0)
                cargas.DensidadLiquido = tanque.DensidadLiquido;
        }

        // Recalcula dimensiones del tanque según geometría
        private void RecalcularGeometria(ProyectoGeneralModel proyecto, TankModel tanque)
        {
            tanque.AlturaPanelBase = _calculoGeometriaService.ObtenerAlturaPanelBase(tanque, proyecto);
            tanque.AlturaTotal = _calculoGeometriaService.ObtenerAlturaTotal(tanque, proyecto);
            tanque.Diametro = _calculoGeometriaService.ObtenerDiametro(tanque, proyecto);
        }

        // Rellena datos del resultado usando el input si faltan
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

        // Calcula el presupuesto de instalación basado en el resultado
        private PresupuestoInstalacionResultadoModel? CalcularPresupuestoInstalacion(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            InstalacionModel instalacion,
            ResultadoCalculoModel resultado)
        {
            try
            {
                // Validaciones básicas
                if (resultado == null || resultado.Anillos == null || resultado.Anillos.Count == 0)
                    return null;

                // Obtiene espesores de los anillos
                var espesores = resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(x => x.EspesorSeleccionado > 0
                        ? Convert.ToDecimal(x.EspesorSeleccionado, CultureInfo.InvariantCulture)
                        : Convert.ToDecimal(x.EspesorRequerido, CultureInfo.InvariantCulture))
                    .Where(x => x > 0)
                    .ToList();

                if (espesores.Count == 0)
                    return null;

                // Construye el input del presupuesto
                var input = new PresupuestoInstalacionInputModel
                {
                    Fabricante = MapearFabricante(proyecto.Fabricante),
                    NumeroPlacasPorAnillo = resultado.ChapasPorAnillo > 0 ? resultado.ChapasPorAnillo : tanque.ChapasPorAnillo,
                    NumeroAnillos = resultado.NumeroAnillos > 0 ? resultado.NumeroAnillos : tanque.NumeroAnillos,
                    TieneStarterRing = instalacion.StarterRing || resultado.TieneStarterRing,
                    TipoTecho = MapearTipoTecho(instalacion.TipoTecho),
                    TipoEscalera = MapearTipoEscalera(instalacion.TipoEscalera),
                    NumeroEscaleras = Math.Max(0, instalacion.NumeroEscaleras),

                    // Conexiones
                    ConexionesDn25a150 = Math.Max(0, instalacion.ConexionesDN25_DN150),
                    ConexionesDn150a300 = Math.Max(0, instalacion.ConexionesDN150_DN300),
                    ConexionesDn300a500 = Math.Max(0, instalacion.ConexionesDN300_DN500),
                    ConexionesMayor500 = Math.Max(0, instalacion.ConexionesMayorDN500),
                    NumeroBocasHombre = Math.Max(1, instalacion.NumeroBocasHombre),
                    NumeroLineasRigidizador = Math.Max(0, resultado.TieneRigidizadorBase ? 1 : 0),

                    // Cuadrilla y tiempos
                    TamanoCuadrilla = Math.Max(1, instalacion.TamanoCuadrilla),
                    HorasTrabajoPorDia = Convert.ToDecimal(
                        instalacion.HorasTrabajoDia > 0 ? instalacion.HorasTrabajoDia : 8,
                        CultureInfo.InvariantCulture),

                    // Lluvia
                    PorcentajeLluvia = Convert.ToDecimal(
                        instalacion.DiasLluviaPorcentaje > 1
                            ? instalacion.DiasLluviaPorcentaje / 100.0
                            : instalacion.DiasLluviaPorcentaje,
                        CultureInfo.InvariantCulture),

                    // Personal
                    NumeroSiteManagers = Math.Max(0, instalacion.SiteManager),
                    NumeroTecnicosSeguridad = Math.Max(0, instalacion.TecnicoSeguridad),
                    UbicacionObra = MapearUbicacion(instalacion.LugarObra),

                    // Geometría
                    DiametroMetros = Convert.ToDecimal(
                        (resultado.Diametro > 0 ? resultado.Diametro : tanque.Diametro) / 1000.0,
                        CultureInfo.InvariantCulture),

                    EspesoresAnillosMm = espesores,
                    DistanciaAlojamientoObraHoras = Convert.ToDecimal(instalacion.DistanciaAlojamientoObra, CultureInfo.InvariantCulture),
                    CosteTransporteManual = Convert.ToDecimal(instalacion.CosteTransporteManual, CultureInfo.InvariantCulture)
                };

                // Validaciones finales
                if (input.NumeroAnillos <= 0 || input.NumeroPlacasPorAnillo <= 0 || input.DiametroMetros <= 0)
                    return null;

                if (input.EspesoresAnillosMm.Count != input.NumeroAnillos)
                    return null;

                // Llama al servicio de cálculo de presupuesto
                return _presupuestoInstalacionExcelService.Calcular(input);
            }
            catch
            {
                // Si falla algo → no rompe el cálculo general
                return null;
            }
        }

        // Métodos auxiliares de mapeo (string → enum)
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

        // Normaliza texto (quita acentos, pasa a minúsculas, etc.)
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