using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;
using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Services.Normativas;
namespace TankDesigner.Core.Services
{



    // Servicio que calcula los anillos del tanque:
    // espesor, plancha, tornillo, configuración y comprobaciones.
    public class CalculoEspesoresService
    {
        private readonly CalculoTanqueService _calculoTanqueService;
        private readonly FormulaEspesoresService _formulaEspesoresService;
        private readonly FormulaPresionService _formulaPresionService;
        private readonly FormulaComprobacionesService _formulaComprobacionesService;
        private readonly CalculoTornilleriaService _calculoTornilleriaService;
        private readonly NormativaFormulaSelectorService _normativaFormulaSelectorService;
        public CalculoEspesoresService()
        {
            _calculoTanqueService = new CalculoTanqueService();
            _formulaEspesoresService = new FormulaEspesoresService();
            _formulaPresionService = new FormulaPresionService();
            _formulaComprobacionesService = new FormulaComprobacionesService();
            _calculoTornilleriaService = new CalculoTornilleriaService();
            _normativaFormulaSelectorService = new NormativaFormulaSelectorService();
        }

        private INormativaFormulaService ObtenerFormulaNormativa(string normativa)
        {
            return _normativaFormulaSelectorService.ObtenerServicio(normativa);
        }
        public List<ResultadoAnilloModel> CalcularAnillos(CalculoTanqueInputModel input, string normativa)
        {
            var resultados = new List<ResultadoAnilloModel>();

            if (input == null)
                return resultados;

            if (input.NumeroAnillos <= 0 || input.Diametro <= 0)
                return resultados;

            List<double> alturasAnillos = ObtenerAlturasAnillos(input);
            double alturaTotalMm = alturasAnillos.Where(a => a > 0).Sum();
            if (alturaTotalMm <= 0)
                alturaTotalMm = input.AlturaTotal;

            if (alturaTotalMm <= 0)
                return resultados;

            ProyectoGeneralModel proyectoBase = new ProyectoGeneralModel
            {
                Fabricante = input.Fabricante,
                MaterialPrincipal = input.MaterialPrincipal
            };

            List<PosibleConfiguracionModel> configuracionesBase = _calculoTanqueService
                .ObtenerConfiguracionesOrdenadas(proyectoBase)
                .Where(c => c != null)
                .ToList();

            if (configuracionesBase.Count == 0)
                return resultados;

            double diametroMm = input.Diametro;
            double diametroM = diametroMm / 1000.0;
            double radioMm = diametroMm / 2.0;
            double cotaInferiorAcumulada = 0;

            for (int i = 0; i < input.NumeroAnillos; i++)
            {
                double alturaAnilloMm = ObtenerAlturaAnillo(input, i, alturasAnillos);
                double alturaInferiorMm = cotaInferiorAcumulada;
                double alturaSuperiorMm = alturaInferiorMm + alturaAnilloMm;
                double alturaCentroMm = (alturaInferiorMm + alturaSuperiorMm) / 2.0;
                cotaInferiorAcumulada = alturaSuperiorMm;

                double alturaLiquidoSobreCentroMm = alturaTotalMm - alturaCentroMm;
                if (alturaLiquidoSobreCentroMm < 0)
                    alturaLiquidoSobreCentroMm = 0;

                double headM = alturaLiquidoSobreCentroMm / 1000.0;
                double presionKPa = _formulaPresionService.CalcularPresionEnAltura(input.DensidadLiquido, alturaLiquidoSobreCentroMm);

                string materialAnillo = ObtenerMaterialAnillo(input, i);
                string materialPreferido = materialAnillo;
                string configuracionPreferida = ObtenerConfiguracionAnillo(input, i);

                var proyectoAnillo = new ProyectoGeneralModel
                {
                    Fabricante = input.Fabricante,
                    MaterialPrincipal = materialAnillo
                };

                List<PosiblePlanchaModel> planchasCandidatas = _calculoTanqueService
                    .ObtenerPlanchasCandidatasOrdenadasPorAnillo(proyectoAnillo, alturaAnilloMm, materialAnillo);

                List<PosibleConfiguracionModel> configuraciones = _calculoTanqueService
                    .ObtenerConfiguracionesOrdenadasPorAnillo(proyectoBase, configuracionPreferida)
                    .Where(c => c != null)
                    .ToList();

                ResultadoAnilloModel mejorResultado = null;

                if (planchasCandidatas.Count > 0 && configuraciones.Count > 0)
                {
                    mejorResultado = ResolverAnilloConPrioridadAwwa(
                        input,
                        normativa,
                        configuraciones,
                        planchasCandidatas,
                        i + 1,
                        alturaInferiorMm,
                        alturaSuperiorMm,
                        alturaCentroMm,
                        headM,
                        presionKPa,
                        diametroM,
                        radioMm);
                }

                if (mejorResultado == null)
                {
                    mejorResultado = new ResultadoAnilloModel
                    {
                        NumeroAnillo = i + 1,
                        AlturaInferior = alturaInferiorMm,
                        AlturaSuperior = alturaSuperiorMm,
                        AlturaCentro = alturaCentroMm,
                        Presion = presionKPa,
                        EspesorRequerido = 0,
                        EspesorSeleccionado = 0,
                        EsValido = false,
                        Mensaje = "No se pudo calcular el anillo",
                        TipoFallo = "DESCONOCIDO",
                        ConfiguracionAplicada = configuracionPreferida,
                        MaterialAplicado = materialPreferido
                    };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(mejorResultado.MaterialAplicado))
                        mejorResultado.MaterialAplicado = materialPreferido;
                }

                resultados.Add(mejorResultado);
            }

            AplicarReglaMonotonia(input, normativa, configuracionesBase, resultados, diametroM, radioMm, alturaTotalMm);

            return resultados;
        }

        private ResultadoAnilloModel ResolverAnilloConPrioridadAwwa(
            CalculoTanqueInputModel input,
            string normativa,
            List<PosibleConfiguracionModel> configuraciones,
            List<PosiblePlanchaModel> planchasCandidatas,
            int numeroAnillo,
            double alturaInferiorMm,
            double alturaSuperiorMm,
            double alturaCentroMm,
            double headM,
            double presionKPa,
            double diametroM,
            double radioMm,
            double espesorMinimoMonotonia = 0,
            double fyMinimoMonotonia = 0)
        {
            ResultadoAnilloModel mejorResultado = null;

            int indicePlancha = 0;

            // Recorremos las planchas candidatas.
            while (indicePlancha < planchasCandidatas.Count)
            {
                PosiblePlanchaModel plancha = planchasCandidatas[indicePlancha];
                if (plancha == null || plancha.Espesor == null || plancha.Espesor.Count == 0)
                {
                    indicePlancha++;
                    continue;
                }

                // Si la plancha no cumple el mínimo de Fy por monotonicidad, se descarta.
                if (plancha.Fy < fyMinimoMonotonia)
                {
                    indicePlancha++;
                    continue;
                }

                double fy = plancha.Fy;
                double fu = plancha.Fu;

                // Se calcula la tensión admisible base y la ajustada por normativa.
                var formulaNormativa = ObtenerFormulaNormativa(normativa);

                double tensionAdmisibleBase = _formulaEspesoresService.CalcularTensionAdmisible(fy, fu);
                double coeficienteNormativa = formulaNormativa.ObtenerCoeficienteEspesor(normativa);
                double tensionAdmisible = formulaNormativa.AjustarTensionAdmisible(tensionAdmisibleBase, normativa);
                // Obtenemos los espesores disponibles de la plancha.
                List<double> espesoresDisponibles = plancha.Espesor
                    .Where(e => e > 0)
                    .OrderBy(e => e)
                    .ToList();

                if (espesoresDisponibles.Count == 0)
                {
                    indicePlancha++;
                    continue;
                }

                // Calculamos el espesor requerido para el anillo.
                double espesorBase = _formulaEspesoresService.CalcularEspesorRequerido(
                    presionKPa,
                    diametroM,
                    tensionAdmisible);

                double espesorRequerido = espesorBase * coeficienteNormativa;
                espesorRequerido = _formulaEspesoresService.AplicarEspesorMinimo(espesorRequerido);

                // Si por monotonicidad hay un mínimo exigido, lo respetamos.
                if (espesorMinimoMonotonia > espesorRequerido)
                    espesorRequerido = espesorMinimoMonotonia;

                int indiceConfiguracion = 0;

                // Recorremos las configuraciones posibles.
                while (indiceConfiguracion < configuraciones.Count)
                {
                    PosibleConfiguracionModel configuracion = configuraciones[indiceConfiguracion];
                    if (configuracion == null)
                    {
                        indiceConfiguracion++;
                        continue;
                    }

                    // Para cada configuración, obtenemos los tornillos ordenados.
                    List<PosibleTornilloModel> tornillos = _calculoTornilleriaService
                        .ObtenerTornillosOrdenadosPorCercania(input, configuracion);

                    if (tornillos == null || tornillos.Count == 0)
                    {
                        indiceConfiguracion++;
                        continue;
                    }

                    ResultadoAnilloModel ultimoResultadoConfiguracion = null;
                    int indiceTornillo = 0;

                    // Recorremos los tornillos.
                    while (indiceTornillo < tornillos.Count)
                    {
                        PosibleTornilloModel tornillo = tornillos[indiceTornillo];
                        if (tornillo == null)
                        {
                            indiceTornillo++;
                            continue;
                        }

                        // Calculamos el anillo con esta combinación concreta.
                        double fuTornillo = ObtenerFuTornillo(tornillo);
                        if (fuTornillo <= 0)
                        {
                            indiceTornillo++;
                            continue;
                        }

                        ResultadoAnilloModel resultadoAnillo = CalcularMejorOpcionAnillo(
                            numeroAnillo,
                            alturaInferiorMm,
                            alturaSuperiorMm,
                            alturaCentroMm,
                            headM,
                            presionKPa,
                            espesorRequerido,
                            espesoresDisponibles,
                            tensionAdmisibleBase,
                            tensionAdmisible,
                            coeficienteNormativa,
                            normativa,
                            radioMm,
                            configuracion.S,
                            configuracion.R,
                            configuracion.DiametroAgujero,
                            tornillo.CalidadTornillo ?? "—",
                            tornillo.Diametro,
                            fuTornillo,
                            fy,
                            fu,
                            configuracion.Nombre ?? "—",
                            string.IsNullOrWhiteSpace(plancha.Material) ? input.MaterialPrincipal : plancha.Material);
                            ultimoResultadoConfiguracion = resultadoAnillo;
                            mejorResultado = ElegirMejorResultado(mejorResultado, resultadoAnillo);

                        // Si ya es válido, devolvemos directamente esa solución.
                        if (resultadoAnillo != null && resultadoAnillo.EsValido)
                            return resultadoAnillo;

                        // Según el tipo de fallo, se decide si seguir probando tornillos
                        // o pasar a otra configuración / plancha.
                        string tipoFallo = DeterminarTipoFallo(resultadoAnillo);

                        if (tipoFallo == "CORTANTE")
                        {
                            indiceTornillo++;
                            continue;
                        }

                        if (tipoFallo == "APLASTAMIENTO")
                            break;

                        if (tipoFallo == "TRACCION")
                            break;

                        indiceTornillo++;
                    }

                    string falloConfiguracion = DeterminarTipoFallo(ultimoResultadoConfiguracion);

                    // Si falla por tracción, normalmente cambiar solo la configuración
                    // ya no mejora suficiente, así que pasamos de plan.
                    if (falloConfiguracion == "TRACCION")
                        break;

                    indiceConfiguracion++;
                }

                indicePlancha++;
            }

            // Si no se encontró ninguna válida, devolvemos la mejor alternativa encontrada.
            return mejorResultado ?? new ResultadoAnilloModel
            {
                NumeroAnillo = numeroAnillo,
                AlturaInferior = alturaInferiorMm,
                AlturaSuperior = alturaSuperiorMm,
                AlturaCentro = alturaCentroMm,
                Presion = presionKPa,
                EspesorRequerido = 0,
                EspesorSeleccionado = 0,
                EsValido = false,
                Mensaje = "No se encontró combinación válida",
                TipoFallo = "DESCONOCIDO"
            };
        }

        private string DeterminarTipoFallo(ResultadoAnilloModel resultado)
        {
            if (resultado == null)
                return "DESCONOCIDO";

            if (resultado.EsValido)
            {
                resultado.TipoFallo = string.Empty;
                return string.Empty;
            }

            // Comprobamos qué verificación es la que falla.
            bool fallaTraccion = resultado.NetTensileStress > resultado.AllowableTensileStress;
            bool fallaAplastamiento = resultado.HoleBearingStress > resultado.AllowableBearingStress;
            bool fallaCortante = resultado.BoltShearStress > resultado.AllowableShearStress;

            if (fallaCortante)
            {
                resultado.TipoFallo = "CORTANTE";
                return "CORTANTE";
            }

            if (fallaAplastamiento)
            {
                resultado.TipoFallo = "APLASTAMIENTO";
                return "APLASTAMIENTO";
            }

            if (fallaTraccion)
            {
                resultado.TipoFallo = "TRACCION";
                return "TRACCION";
            }

            resultado.TipoFallo = "DESCONOCIDO";
            return "DESCONOCIDO";
        }

        private ResultadoAnilloModel CalcularMejorOpcionAnillo(
            int numeroAnillo,
            double alturaInferiorMm,
            double alturaSuperiorMm,
            double alturaCentroMm,
            double headM,
            double presionKPa,
            double espesorRequerido,
            List<double> espesoresDisponibles,
            double tensionAdmisibleBase,
            double tensionAdmisible,
            double coeficienteNormativa,
            string normativa,
            double radioMm,
            double pasoS,
            double relacionR,
            double diametroAgujero,
            string nombreTornillo,
            double diametroTornillo,
            double fuTornillo,
            double fy,
            double fu,
            string nombreConfiguracion,
            string materialPlancha)
        {
            // Filtramos espesores disponibles que sean suficientes.
            var espesoresValidos = espesoresDisponibles
                .Where(e => e >= espesorRequerido)
                .OrderBy(e => e)
                .ToList();

            // Si no hay ninguno, se devuelve resultado inválido.
            if (espesoresValidos.Count == 0)
            {
                var resultadoSinEspesor = new ResultadoAnilloModel
                {
                    NumeroAnillo = numeroAnillo,
                    AlturaInferior = alturaInferiorMm,
                    AlturaSuperior = alturaSuperiorMm,
                    AlturaCentro = alturaCentroMm,
                    Presion = presionKPa,
                    EspesorRequerido = espesorRequerido,
                    EspesorSeleccionado = 0,
                    EsValido = false,
                    Mensaje = $"No hay espesor disponible ≥ {espesorRequerido:0.##} mm ({NormalizarNombreNormativa(normativa)})",
                    Head = headM,
                    TensionAdmisibleBase = tensionAdmisibleBase,
                    TensionAdmisibleAjustada = tensionAdmisible,
                    CoeficienteNormativa = coeficienteNormativa,
                    NormativaAplicada = NormalizarNombreNormativa(normativa),
                    TornilloAplicado = nombreTornillo,
                    DiametroTornilloAplicado = diametroTornillo,
                    ConfiguracionAplicada = nombreConfiguracion,
                    MaterialAplicado = materialPlancha,
                    DiametroAgujero = diametroAgujero,
                    FyPlancha = fy,
                    FuPlancha = fu,
                    PasoS = pasoS,
                    RelacionR = relacionR,
                    CumpleTraccion = false,
                    CumpleAplastamiento = false,
                    CumpleCortante = false
                };

                DeterminarTipoFallo(resultadoSinEspesor);
                resultadoSinEspesor.EstadoResumen = ConstruirResumenEstado(resultadoSinEspesor);
                return resultadoSinEspesor;
            }

            ResultadoAnilloModel mejorResultado = null;

            // Coeficientes extra aplicados cuando la normativa es AWWA.
            var formulaNormativa = ObtenerFormulaNormativa(normativa);

            double coeficienteSeguridadAwwa = formulaNormativa.ObtenerCoeficienteSeguridadGeneral(normativa);
            double coeficienteSeguridadConfiguracionTornillosAwwa = formulaNormativa.ObtenerCoeficienteSeguridadTornilleria(normativa);

            // Probamos cada espesor válido hasta encontrar uno que cumpla.
            foreach (double espesorSeleccionado in espesoresValidos)
            {
                // Cálculos mecánicos del anillo.
                double hydrostaticHoopLoad = _formulaComprobacionesService.CalcularHydrostaticHoopLoad(
                    headM,
                    radioMm);

                double netTensileStress = _formulaComprobacionesService.CalcularNetTensileStress(
                    hydrostaticHoopLoad,
                    pasoS,
                    espesorSeleccionado,
                    diametroAgujero);

                double allowableTensileStress = _formulaComprobacionesService.CalcularAllowableTensileStress(
                    fy,
                    fu,
                    relacionR,
                    diametroTornillo,
                    pasoS);

                double holeBearingStress = _formulaComprobacionesService.CalcularHoleBearingStress(
                    hydrostaticHoopLoad,
                    pasoS,
                    relacionR,
                    diametroTornillo,
                    espesorSeleccionado);

                double allowableBearingStress = _formulaComprobacionesService.CalcularAllowableBearingStress(fy);

                double boltShearStress = _formulaComprobacionesService.CalcularBoltShearStress(
                    hydrostaticHoopLoad,
                    pasoS,
                    relacionR,
                    diametroTornillo);

                double allowableShearStress = _formulaComprobacionesService.CalcularAllowableShearStress(fuTornillo);

                // Aplicamos los coeficientes de seguridad.
                double netTensileStressComprobacion = netTensileStress * coeficienteSeguridadAwwa;
                double holeBearingStressComprobacion = holeBearingStress * coeficienteSeguridadAwwa * coeficienteSeguridadConfiguracionTornillosAwwa;
                double boltShearStressComprobacion = boltShearStress * coeficienteSeguridadAwwa * coeficienteSeguridadConfiguracionTornillosAwwa;

                // Comprobamos si cumple cada verificación.
                bool cumpleTraccion = netTensileStressComprobacion <= allowableTensileStress;
                bool cumpleAplastamiento = holeBearingStressComprobacion <= allowableBearingStress;
                bool cumpleCortante = boltShearStressComprobacion <= allowableShearStress;

                bool esValido = cumpleTraccion && cumpleAplastamiento && cumpleCortante;

                var resultado = new ResultadoAnilloModel
                {
                    NumeroAnillo = numeroAnillo,
                    AlturaInferior = alturaInferiorMm,
                    AlturaSuperior = alturaSuperiorMm,
                    AlturaCentro = alturaCentroMm,
                    Presion = presionKPa,
                    EspesorRequerido = espesorRequerido,
                    EspesorSeleccionado = espesorSeleccionado,
                    EsValido = esValido,

                    Mensaje = esValido
                        ? $"OK ({formulaNormativa.ObtenerNombreNormativa(normativa)})"
                        : $"No cumple comprobaciones: {ConstruirMensajeFallo(netTensileStressComprobacion, allowableTensileStress, holeBearingStressComprobacion, allowableBearingStress, boltShearStressComprobacion, allowableShearStress)} ({formulaNormativa.ObtenerNombreNormativa(normativa)})",

                    Head = headM,
                    TensionAdmisibleBase = tensionAdmisibleBase,
                    TensionAdmisibleAjustada = tensionAdmisible,
                    CoeficienteNormativa = coeficienteNormativa,
                    NormativaAplicada = formulaNormativa.ObtenerNombreNormativa(normativa),

                    HydrostaticHoopLoad = hydrostaticHoopLoad,
                    NetTensileStress = netTensileStressComprobacion,
                    AllowableTensileStress = allowableTensileStress,
                    HoleBearingStress = holeBearingStressComprobacion,
                    AllowableBearingStress = allowableBearingStress,
                    BoltShearStress = boltShearStressComprobacion,
                    AllowableShearStress = allowableShearStress,

                    TornilloAplicado = nombreTornillo,
                    DiametroTornilloAplicado = diametroTornillo,
                    ConfiguracionAplicada = nombreConfiguracion,
                    MaterialAplicado = materialPlancha,
                    DiametroAgujero = diametroAgujero,
                    FyPlancha = fy,
                    FuPlancha = fu,
                    PasoS = pasoS,
                    RelacionR = relacionR,

                    CumpleTraccion = cumpleTraccion,
                    CumpleAplastamiento = cumpleAplastamiento,
                    CumpleCortante = cumpleCortante
                };

                DeterminarTipoFallo(resultado);
                formulaNormativa.AplicarParametrosAnillo(resultado);
                resultado.EstadoResumen = ConstruirResumenEstado(resultado);

                // Si ya cumple, devolvemos directamente.
                if (resultado.EsValido)
                    return resultado;

                // Si no cumple, nos quedamos con la mejor opción inválida.
                mejorResultado = ElegirMejorResultado(mejorResultado, resultado);
            }

            return mejorResultado;
        }

        private string ConstruirMensajeFallo(
            double netTensileStress,
            double allowableTensileStress,
            double holeBearingStress,
            double allowableBearingStress,
            double boltShearStress,
            double allowableShearStress)
        {
            var fallos = new List<string>();

            if (netTensileStress > allowableTensileStress)
                fallos.Add("tracción");

            if (holeBearingStress > allowableBearingStress)
                fallos.Add("aplastamiento");

            if (boltShearStress > allowableShearStress)
                fallos.Add("cortante");

            if (fallos.Count == 0)
                return "sin fallo identificado";

            return string.Join(", ", fallos);
        }

        private string ConstruirResumenEstado(ResultadoAnilloModel resultado)
        {
            if (resultado == null)
                return "Sin resultado";

            if (resultado.EsValido)
                return "Válido";

            if (!string.IsNullOrWhiteSpace(resultado.TipoFallo))
                return $"Inválido - {resultado.TipoFallo}";

            return "Inválido";
        }

        private void AplicarReglaMonotonia(
            CalculoTanqueInputModel input,
            string normativa,
            List<PosibleConfiguracionModel> configuracionesBase,
            List<ResultadoAnilloModel> resultados,
            double diametroM,
            double radioMm,
            double alturaTotalMm)
        {
            // La monotonicidad obliga a que un anillo inferior no sea "peor"
            // que el anillo superior en espesor y Fy.
            if (resultados == null || resultados.Count <= 1)
                return;

            if (resultados.Any(r => r == null))
                return;

            bool huboCambios;
            int iteraciones = 0;
            int maxIteraciones = resultados.Count * 3;

            do
            {
                huboCambios = false;
                iteraciones++;

                for (int i = resultados.Count - 2; i >= 0; i--)
                {
                    ResultadoAnilloModel anilloInferior = resultados[i];
                    ResultadoAnilloModel anilloSuperior = resultados[i + 1];

                    if (anilloInferior == null || anilloSuperior == null)
                        continue;

                    bool fallaEspesor = anilloInferior.EspesorSeleccionado < anilloSuperior.EspesorSeleccionado;
                    bool fallaFy = anilloInferior.FyPlancha < anilloSuperior.FyPlancha;

                    if (!fallaEspesor && !fallaFy)
                        continue;

                    // Intentamos recalcular el anillo para corregir la monotonicidad.
                    ResultadoAnilloModel recalculado = RecalcularAnilloPorMonotonia(
                        input,
                        normativa,
                        configuracionesBase,
                        anilloInferior,
                        i,
                        anilloSuperior.EspesorSeleccionado,
                        anilloSuperior.FyPlancha,
                        diametroM,
                        radioMm,
                        alturaTotalMm);

                    if (recalculado != null &&
                        recalculado.EsValido &&
                        CumpleMonotonia(recalculado, anilloSuperior))
                    {
                        bool cambia =
                            anilloInferior.EspesorSeleccionado != recalculado.EspesorSeleccionado ||
                            anilloInferior.FyPlancha != recalculado.FyPlancha ||
                            anilloInferior.DiametroTornilloAplicado != recalculado.DiametroTornilloAplicado ||
                            anilloInferior.ConfiguracionAplicada != recalculado.ConfiguracionAplicada;

                        recalculado.Mensaje = $"Recalculado automáticamente por monotonicidad ({recalculado.ConfiguracionAplicada} / {recalculado.TornilloAplicado})";
                        resultados[i] = recalculado;

                        if (cambia)
                            huboCambios = true;

                        continue;
                    }

                    bool corregido = false;

                    // Si no se pudo recalcular completo, al menos se fuerza el espesor.
                    if (anilloInferior.EspesorSeleccionado < anilloSuperior.EspesorSeleccionado)
                    {
                        anilloInferior.EspesorSeleccionado = anilloSuperior.EspesorSeleccionado;
                        corregido = true;
                    }

                    if (corregido)
                    {
                        RecalcularComprobacionesConEspesorCorregido(anilloInferior);

                        if (fallaFy)
                        {
                            anilloInferior.EsValido = false;
                            anilloInferior.Mensaje = "Espesor corregido por monotonicidad, pero Fy inferior al anillo superior y no se pudo recalcular";
                        }
                        else
                        {
                            anilloInferior.Mensaje = anilloInferior.EsValido
                                ? "Corregido por monotonicidad de espesor"
                                : "Corregido por monotonicidad de espesor, pero no cumple comprobaciones";
                        }

                        huboCambios = true;
                    }
                    else if (fallaFy)
                    {
                        anilloInferior.EsValido = false;
                        anilloInferior.Mensaje = "No cumple monotonicidad de Fy y no se pudo autocorregir";
                    }
                }

            } while (huboCambios && iteraciones < maxIteraciones);
        }

        private ResultadoAnilloModel RecalcularAnilloPorMonotonia(
            CalculoTanqueInputModel input,
            string normativa,
            List<PosibleConfiguracionModel> configuracionesBase,
            ResultadoAnilloModel resultadoOriginal,
            int indiceAnillo,
            double espesorMinimoMonotonia,
            double fyMinimoMonotonia,
            double diametroM,
            double radioMm,
            double alturaTotalMm)
        {
            List<double> alturasAnillos = ObtenerAlturasAnillos(input);
            double alturaAnilloMm = ObtenerAlturaAnillo(input, indiceAnillo, alturasAnillos);
            double alturaInferiorMm = alturasAnillos.Take(indiceAnillo).Where(a => a > 0).Sum();
            double alturaSuperiorMm = alturaInferiorMm + alturaAnilloMm;
            double alturaCentroMm = (alturaInferiorMm + alturaSuperiorMm) / 2.0;

            double alturaLiquidoSobreCentroMm = alturaTotalMm - alturaCentroMm;
            if (alturaLiquidoSobreCentroMm < 0)
                alturaLiquidoSobreCentroMm = 0;

            double headM = alturaLiquidoSobreCentroMm / 1000.0;

            double presionKPa = _formulaPresionService.CalcularPresionEnAltura(input.DensidadLiquido, alturaLiquidoSobreCentroMm);

            string materialAnillo = ObtenerMaterialAnillo(input, indiceAnillo);
            string configuracionPreferidaTexto = ObtenerConfiguracionAnillo(input, indiceAnillo);

            var proyectoAnillo = new ProyectoGeneralModel
            {
                Fabricante = input.Fabricante,
                MaterialPrincipal = materialAnillo
            };

            List<PosiblePlanchaModel> planchasCandidatas = _calculoTanqueService
                .ObtenerPlanchasCandidatasOrdenadasPorAnillo(proyectoAnillo, alturaAnilloMm, materialAnillo);

            List<PosibleConfiguracionModel> configuraciones = _calculoTanqueService
                .ObtenerConfiguracionesOrdenadasPorAnillo(
                    new ProyectoGeneralModel { Fabricante = input.Fabricante, MaterialPrincipal = input.MaterialPrincipal },
                    !string.IsNullOrWhiteSpace(configuracionPreferidaTexto) ? configuracionPreferidaTexto : resultadoOriginal?.ConfiguracionAplicada ?? string.Empty)
                .Where(c => c != null)
                .ToList();

            ResultadoAnilloModel recalculoPreferente = ResolverAnilloConConfiguracionPreferida(
                input,
                normativa,
                configuraciones,
                planchasCandidatas,
                resultadoOriginal,
                indiceAnillo + 1,
                alturaInferiorMm,
                alturaSuperiorMm,
                alturaCentroMm,
                headM,
                presionKPa,
                diametroM,
                radioMm,
                espesorMinimoMonotonia,
                fyMinimoMonotonia);

            if (recalculoPreferente != null && recalculoPreferente.EsValido)
                return recalculoPreferente;

            // Si no, recalculamos de forma normal buscando la mejor opción.
            return ResolverAnilloConPrioridadAwwa(
                input,
                normativa,
                configuraciones,
                planchasCandidatas,
                indiceAnillo + 1,
                alturaInferiorMm,
                alturaSuperiorMm,
                alturaCentroMm,
                headM,
                presionKPa,
                diametroM,
                radioMm,
                espesorMinimoMonotonia,
                fyMinimoMonotonia);
        }

        private ResultadoAnilloModel ResolverAnilloConConfiguracionPreferida(
            CalculoTanqueInputModel input,
            string normativa,
            List<PosibleConfiguracionModel> configuraciones,
            List<PosiblePlanchaModel> planchasCandidatas,
            ResultadoAnilloModel resultadoOriginal,
            int numeroAnillo,
            double alturaInferiorMm,
            double alturaSuperiorMm,
            double alturaCentroMm,
            double headM,
            double presionKPa,
            double diametroM,
            double radioMm,
            double espesorMinimoMonotonia,
            double fyMinimoMonotonia)
        {
            if (resultadoOriginal == null || configuraciones == null || configuraciones.Count == 0)
                return null;

            // Intentamos usar la misma configuración del resultado anterior.
            PosibleConfiguracionModel configuracionPreferida =
                ObtenerConfiguracionDesdeResultado(resultadoOriginal, configuraciones);

            if (configuracionPreferida == null)
                return null;

            ResultadoAnilloModel mejorResultado = null;

            // Filtramos planchas que cumplan el Fy mínimo.
            List<PosiblePlanchaModel> planchasFiltradas = planchasCandidatas
                .Where(p => p != null &&
                            p.Espesor != null &&
                            p.Espesor.Count > 0 &&
                            p.Fy >= fyMinimoMonotonia)
                .ToList();

            if (planchasFiltradas.Count == 0)
                return null;

            foreach (PosiblePlanchaModel plancha in planchasFiltradas)
            {
                double fy = plancha.Fy;
                double fu = plancha.Fu;

                var formulaNormativa = ObtenerFormulaNormativa(normativa);

                double tensionAdmisibleBase = _formulaEspesoresService.CalcularTensionAdmisible(fy, fu);
                double coeficienteNormativa = formulaNormativa.ObtenerCoeficienteEspesor(normativa);
                double tensionAdmisible = formulaNormativa.AjustarTensionAdmisible(tensionAdmisibleBase, normativa);

                List<double> espesoresDisponibles = plancha.Espesor
                    .Where(e => e > 0)
                    .OrderBy(e => e)
                    .ToList();

                if (espesoresDisponibles.Count == 0)
                    continue;

                double espesorBase = _formulaEspesoresService.CalcularEspesorRequerido(
                    presionKPa,
                    diametroM,
                    tensionAdmisible);

                double espesorRequerido = espesorBase * coeficienteNormativa;
                espesorRequerido = _formulaEspesoresService.AplicarEspesorMinimo(espesorRequerido);

                if (espesorMinimoMonotonia > espesorRequerido)
                    espesorRequerido = espesorMinimoMonotonia;

                List<PosibleTornilloModel> tornillos = _calculoTornilleriaService
                    .ObtenerTornillosOrdenadosPorCercania(input, configuracionPreferida);

                if (tornillos == null || tornillos.Count == 0)
                    continue;

                // Empezamos a probar por el tornillo más parecido al que ya tenía.
                int indiceInicialTornillo = ObtenerIndiceTornilloPreferido(tornillos, resultadoOriginal);
                if (indiceInicialTornillo < 0)
                    indiceInicialTornillo = 0;

                for (int i = indiceInicialTornillo; i < tornillos.Count; i++)
                {
                    PosibleTornilloModel tornillo = tornillos[i];
                    if (tornillo == null)
                        continue;

                    ResultadoAnilloModel resultado = CalcularMejorOpcionAnillo(
                       numeroAnillo,
                       alturaInferiorMm,
                       alturaSuperiorMm,
                       alturaCentroMm,
                       headM,
                       presionKPa,
                       espesorRequerido,
                       espesoresDisponibles,
                       tensionAdmisibleBase,
                       tensionAdmisible,
                       coeficienteNormativa,
                       normativa,
                       radioMm,
                       configuracionPreferida.S,
                       configuracionPreferida.R,
                       configuracionPreferida.DiametroAgujero,
                       tornillo.CalidadTornillo ?? "No definido",
                       tornillo.Diametro,
                       ObtenerFuTornillo(tornillo),
                       fy,
                       fu,
                       configuracionPreferida.Nombre ?? "No definida",
                       string.IsNullOrWhiteSpace(plancha.Material) ? input.MaterialPrincipal : plancha.Material);

                    mejorResultado = ElegirMejorResultado(mejorResultado, resultado);

                    if (resultado != null && resultado.EsValido)
                    {
                        resultado.Mensaje = "Recalculado desde configuración preferida";
                        return resultado;
                    }

                    string tipoFallo = DeterminarTipoFallo(resultado);

                    // Si falla por estos motivos, no suele tener sentido seguir
                    // con más tornillos en esta misma configuración.
                    if (tipoFallo == "APLASTAMIENTO" || tipoFallo == "TRACCION")
                        break;
                }
            }

            return mejorResultado;
        }

        private int ObtenerIndiceTornilloPreferido(
            List<PosibleTornilloModel> tornillos,
            ResultadoAnilloModel resultadoOriginal)
        {
            if (tornillos == null || tornillos.Count == 0 || resultadoOriginal == null)
                return 0;

            // Busca el tornillo que más se parece al que ya tenía el resultado original.
            for (int i = 0; i < tornillos.Count; i++)
            {
                PosibleTornilloModel tornillo = tornillos[i];
                if (tornillo == null)
                    continue;

                bool mismoNombre =
                    !string.IsNullOrWhiteSpace(resultadoOriginal.TornilloAplicado) &&
                    !string.IsNullOrWhiteSpace(tornillo.CalidadTornillo) &&
                    tornillo.CalidadTornillo.Equals(
                        resultadoOriginal.TornilloAplicado,
                        System.StringComparison.OrdinalIgnoreCase);

                bool mismoDiametro =
                    tornillo.Diametro == resultadoOriginal.DiametroTornilloAplicado;

                if (mismoNombre || mismoDiametro)
                    return i;
            }

            return 0;
        }

        private ResultadoAnilloModel ElegirMejorResultado(
            ResultadoAnilloModel actual,
            ResultadoAnilloModel candidato)
        {
            if (candidato == null)
                return actual;

            if (actual == null)
                return candidato;

            // Siempre preferimos uno válido a uno no válido.
            if (actual.EsValido && !candidato.EsValido)
                return actual;

            if (!actual.EsValido && candidato.EsValido)
                return candidato;

            // Si ambos son válidos, elegimos el más "ligero" o económico técnicamente:
            // menos espesor, menos Fy, menor tornillo, etc.
            if (actual.EsValido && candidato.EsValido)
            {
                int comparacionEspesor = actual.EspesorSeleccionado.CompareTo(candidato.EspesorSeleccionado);
                if (comparacionEspesor != 0)
                    return comparacionEspesor <= 0 ? actual : candidato;

                int comparacionFy = actual.FyPlancha.CompareTo(candidato.FyPlancha);
                if (comparacionFy != 0)
                    return comparacionFy <= 0 ? actual : candidato;

                int comparacionDiametroTornillo = actual.DiametroTornilloAplicado.CompareTo(candidato.DiametroTornilloAplicado);
                if (comparacionDiametroTornillo != 0)
                    return comparacionDiametroTornillo <= 0 ? actual : candidato;

                int comparacionPaso = actual.PasoS.CompareTo(candidato.PasoS);
                if (comparacionPaso != 0)
                    return comparacionPaso <= 0 ? actual : candidato;

                return actual;
            }

            // Si ambos son inválidos, nos quedamos con el que menos se pasa de los límites.
            double puntuacionActual = CalcularPuntuacionInvalida(actual);
            double puntuacionCandidata = CalcularPuntuacionInvalida(candidato);

            return puntuacionCandidata < puntuacionActual ? candidato : actual;
        }

        private double CalcularPuntuacionInvalida(ResultadoAnilloModel resultado)
        {
            if (resultado == null)
                return double.MaxValue;

            double penalizacion = 0;

            // Se penaliza cuánto se pasa de cada límite.
            penalizacion += ObtenerExcesoRatio(resultado.NetTensileStress, resultado.AllowableTensileStress) * 1000.0;
            penalizacion += ObtenerExcesoRatio(resultado.HoleBearingStress, resultado.AllowableBearingStress) * 1000.0;
            penalizacion += ObtenerExcesoRatio(resultado.BoltShearStress, resultado.AllowableShearStress) * 1000.0;

            // Y también se penalizan soluciones más grandes o pesadas.
            penalizacion += resultado.EspesorSeleccionado;
            penalizacion += resultado.FyPlancha * 0.01;
            penalizacion += resultado.DiametroTornilloAplicado * 0.1;

            return penalizacion;
        }

        private double ObtenerExcesoRatio(double valor, double limite)
        {
            if (limite <= 0)
                return 1000;

            if (valor <= limite)
                return 0;

            return (valor - limite) / limite;
        }

        private bool CumpleMonotonia(ResultadoAnilloModel anilloInferior, ResultadoAnilloModel anilloSuperior)
        {
            if (anilloInferior == null || anilloSuperior == null)
                return false;

            return anilloInferior.EspesorSeleccionado >= anilloSuperior.EspesorSeleccionado
                && anilloInferior.FyPlancha >= anilloSuperior.FyPlancha;
        }

        private PosibleConfiguracionModel ObtenerConfiguracionDesdeResultado(
            ResultadoAnilloModel resultado,
            List<PosibleConfiguracionModel> configuraciones)
        {
            if (resultado == null || configuraciones == null || configuraciones.Count == 0)
                return null;

            // Intenta encontrar la configuración del catálogo a partir de los datos guardados en el resultado.
            return configuraciones.FirstOrDefault(c =>
                c != null &&
                System.Math.Abs(c.S - resultado.PasoS) < 0.0001 &&
                System.Math.Abs(c.R - resultado.RelacionR) < 0.0001 &&
                System.Math.Abs(c.DiametroAgujero - resultado.DiametroAgujero) < 0.0001);
        }

        private void RecalcularComprobacionesConEspesorCorregido(ResultadoAnilloModel anillo)
        {
            if (anillo == null)
                return;

            // Si el espesor no es válido, no se puede recalcular.
            if (anillo.EspesorSeleccionado <= 0)
            {
                anillo.EsValido = false;
                anillo.TipoFallo = "DESCONOCIDO";
                anillo.EstadoResumen = ConstruirResumenEstado(anillo);
                return;
            }

            double diametroTornillo = anillo.DiametroTornilloAplicado;
            double diametroAgujero = anillo.DiametroAgujero;
            double fy = anillo.FyPlancha;
            double fu = anillo.FuPlancha;

            double pasoS = ObtenerPasoSDesdeResultado(anillo);
            double relacionR = ObtenerRelacionRDesdeResultado(anillo);

            if (pasoS <= 0 || relacionR <= 0 || diametroTornillo <= 0 || diametroAgujero <= 0)
            {
                anillo.EsValido = false;
                anillo.Mensaje = "No se pudo recalcular la monotonicidad por falta de datos";
                anillo.TipoFallo = "DESCONOCIDO";
                anillo.EstadoResumen = ConstruirResumenEstado(anillo);
                return;
            }

            // Rehacemos las comprobaciones con el nuevo espesor.
            double allowableTensileStress = _formulaComprobacionesService.CalcularAllowableTensileStress(
                fy,
                fu,
                relacionR,
                diametroTornillo,
                pasoS);

            double holeBearingStress = _formulaComprobacionesService.CalcularHoleBearingStress(
                anillo.HydrostaticHoopLoad,
                pasoS,
                relacionR,
                diametroTornillo,
                anillo.EspesorSeleccionado);

            double allowableBearingStress = _formulaComprobacionesService.CalcularAllowableBearingStress(fy);

            double netTensileStress = _formulaComprobacionesService.CalcularNetTensileStress(
                anillo.HydrostaticHoopLoad,
                pasoS,
                anillo.EspesorSeleccionado,
                diametroAgujero);

            double boltShearStressBase = _formulaComprobacionesService.CalcularBoltShearStress(
                anillo.HydrostaticHoopLoad,
                pasoS,
                relacionR,
                diametroTornillo);

            var formulaNormativa = ObtenerFormulaNormativa(anillo.NormativaAplicada);

            double coeficienteSeguridadAwwa = formulaNormativa.ObtenerCoeficienteSeguridadGeneral(anillo.NormativaAplicada);
            double coeficienteSeguridadConfiguracionTornillosAwwa = formulaNormativa.ObtenerCoeficienteSeguridadTornilleria(anillo.NormativaAplicada);

            double netTensileStressComprobacion = netTensileStress * coeficienteSeguridadAwwa;
            double holeBearingStressComprobacion = holeBearingStress * coeficienteSeguridadAwwa * coeficienteSeguridadConfiguracionTornillosAwwa;
            double boltShearStressComprobacion = boltShearStressBase * coeficienteSeguridadAwwa * coeficienteSeguridadConfiguracionTornillosAwwa;

            anillo.NetTensileStress = netTensileStressComprobacion;
            anillo.AllowableTensileStress = allowableTensileStress;
            anillo.HoleBearingStress = holeBearingStressComprobacion;
            anillo.AllowableBearingStress = allowableBearingStress;
            anillo.BoltShearStress = boltShearStressComprobacion;

            bool cumpleTraccion = netTensileStressComprobacion <= allowableTensileStress;
            bool cumpleAplastamiento = holeBearingStressComprobacion <= allowableBearingStress;
            bool cumpleCortante = boltShearStressComprobacion <= anillo.AllowableShearStress;

            anillo.CumpleTraccion = cumpleTraccion;
            anillo.CumpleAplastamiento = cumpleAplastamiento;
            anillo.CumpleCortante = cumpleCortante;

            anillo.EsValido = cumpleTraccion && cumpleAplastamiento && cumpleCortante;

            DeterminarTipoFallo(anillo);
            formulaNormativa.AplicarParametrosAnillo(anillo);
            anillo.EstadoResumen = ConstruirResumenEstado(anillo);
        }

        private double ObtenerCoeficienteNormativa(string normativa)
        {
            // Coeficiente general según normativa.
            // Si no hay normativa válida, no se fuerza ningún coeficiente extra.
            if (string.IsNullOrWhiteSpace(normativa))
                return 1.0;

            string n = normativa.Trim().ToUpper();

            if (n.Contains("AWWA"))
                return 1.00;

            if (n.Contains("EC") || n.Contains("EUROCODE"))
                return 1.10;

            if (n.Contains("ISO"))
                return 1.05;

            return 1.0;
        }

        private double ObtenerPasoSDesdeResultado(ResultadoAnilloModel anillo)
        {
            if (anillo == null)
                return 0;

            return anillo.PasoS > 0 ? anillo.PasoS : 0;
        }

        private double ObtenerRelacionRDesdeResultado(ResultadoAnilloModel anillo)
        {
            if (anillo == null)
                return 0;

            return anillo.RelacionR > 0 ? anillo.RelacionR : 0;
        }
        private double AjustarTensionAdmisiblePorNormativa(double tensionAdmisibleBase, string normativa)
        {
            if (tensionAdmisibleBase <= 0)
                return 0;

            if (string.IsNullOrWhiteSpace(normativa))
                return tensionAdmisibleBase;

            string n = normativa.Trim().ToUpper();

            // AWWA usa la base sin corregir.
            if (n.Contains("AWWA"))
                return tensionAdmisibleBase;

            if (n.Contains("EC") || n.Contains("EUROCODE"))
                return tensionAdmisibleBase * 0.95;

            return tensionAdmisibleBase * 0.98;
        }

        private string NormalizarNombreNormativa(string normativa)
        {
            if (string.IsNullOrWhiteSpace(normativa))
                return "—";

            string n = normativa.Trim().ToUpper();

            if (n.Contains("AWWA"))
                return "AWWA";

            if (n.Contains("EC") || n.Contains("EUROCODE"))
                return "EC";

            if (n.Contains("ISO"))
                return "ISO";

            return normativa.Trim();
        }

        private double ObtenerCoeficienteSeguridadAwwa(string normativa)
        {
            // Coeficiente extra usado en comprobaciones para AWWA.
            if (string.IsNullOrWhiteSpace(normativa))
                return 1.0;

            string n = normativa.Trim().ToUpper();

            if (n.Contains("AWWA"))
                return 1.5;

            return 1.0;
        }

        private double ObtenerCoeficienteSeguridadConfiguracionTornillosAwwa(string normativa)
        {
            // Coeficiente extra para las comprobaciones relacionadas con tornillería en AWWA.
            if (string.IsNullOrWhiteSpace(normativa))
                return 1.0;

            string n = normativa.Trim().ToUpper();

            if (n.Contains("AWWA"))
                return 1.2;

            return 1.0;
        }


        private List<double> ObtenerAlturasAnillos(CalculoTanqueInputModel input)
        {
            var lista = new List<double>();

            if (input?.AlturasAnillos != null)
                lista.AddRange(input.AlturasAnillos.Where(a => a > 0));

            if (input?.Anillos != null && input.Anillos.Count > 0)
            {
                foreach (var anillo in input.Anillos.OrderBy(a => a.NumeroAnillo))
                {
                    if (anillo != null && anillo.AlturaMm > 0 && lista.Count < input.NumeroAnillos)
                        lista.Add(anillo.AlturaMm);
                }
            }

            while (lista.Count < Math.Max(0, input?.NumeroAnillos ?? 0))
                lista.Add(input?.AlturaPanelBase ?? 0);

            if ((input?.NumeroAnillos ?? 0) > 0 && lista.Count > input!.NumeroAnillos)
                lista = lista.Take(input.NumeroAnillos).ToList();

            return lista;
        }

        private double ObtenerAlturaAnillo(CalculoTanqueInputModel input, int indice, List<double> alturasAnillos)
        {
            if (alturasAnillos != null && indice >= 0 && indice < alturasAnillos.Count && alturasAnillos[indice] > 0)
                return alturasAnillos[indice];

            return input?.AlturaPanelBase ?? 0;
        }

        private string ObtenerMaterialAnillo(CalculoTanqueInputModel input, int indice)
        {
            if (input?.MaterialesAnillos != null && indice >= 0 && indice < input.MaterialesAnillos.Count)
            {
                string valor = (input.MaterialesAnillos[indice] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor;
            }

            if (input?.Anillos != null && indice >= 0 && indice < input.Anillos.Count)
            {
                string valor = (input.Anillos[indice]?.Material ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor;
            }

            return input?.MaterialPrincipal ?? string.Empty;
        }

        private string ObtenerConfiguracionAnillo(CalculoTanqueInputModel input, int indice)
        {
            if (input?.ConfiguracionesAnillos != null && indice >= 0 && indice < input.ConfiguracionesAnillos.Count)
            {
                string valor = (input.ConfiguracionesAnillos[indice] ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor;
            }

            if (input?.Anillos != null && indice >= 0 && indice < input.Anillos.Count)
            {
                string valor = (input.Anillos[indice]?.Configuracion ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(valor))
                    return valor;
            }

            return string.Empty;
        }

        private double ObtenerFuTornillo(PosibleTornilloModel tornillo)
        {
            if (tornillo == null)
                return 0;

            // Si el tornillo trae su Fu real, se usa.
            if (tornillo.FuTornillos > 0)
                return tornillo.FuTornillos;

            // Si no existe Fu real en catálogo, no se inventa.
            return 0;
        }
    }
}

