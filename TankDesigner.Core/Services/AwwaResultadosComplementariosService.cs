using System.Globalization;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services
{
    // Servicio que completa resultados adicionales para normativa AWWA.
    // Calcula comprobaciones de axial, viento, sismo y combinación sísmica.
    public class AwwaResultadosComplementariosService
    {
        private const double DensidadAceroAwwa = 95.0; // kN/m3
        private const double CoefSeguridadAwwa = 1.0;
        private const double CoefSeguridadTornillosAwwa = 1.0;
        private const double CoefFuerzaVientoVirola = 0.6;
        private const double CoefFuerzaVientoTecho = 0.5;

        // Punto de entrada del servicio.
        // Solo actúa si hay resultado válido de anillos y la normativa es AWWA.
        public void Completar(CalculoTanqueInputModel input, ResultadoCalculoModel resultado)
        {
            if (input == null || resultado == null || resultado.Anillos == null || resultado.Anillos.Count == 0)
                return;

            if (!EsAwwa(input.Normativa) && !EsAwwa(resultado.Normativa))
                return;

            // Ordena los anillos de arriba a abajo para aplicar las cargas acumuladas.
            List<ResultadoAnilloModel> anillosTopDown = resultado.Anillos
                .OrderByDescending(a => a.NumeroAnillo)
                .ToList();

            double diametroMm = input.Diametro > 0 ? input.Diametro : resultado.Diametro;
            double radioMm = diametroMm / 2.0;
            double diametroM = diametroMm / 1000.0;
            double alturaTotalMm = input.AlturaTotal > 0 ? input.AlturaTotal : resultado.AlturaTotal;
            double alturaLiquidoMm = Math.Max(0.0, alturaTotalMm - (input.BordeLibre > 0 ? input.BordeLibre : resultado.BordeLibre));

            // Si no se ha indicado el área proyectada de cubierta, la calcula con el diámetro.
            double roofAreaM2 = input.RoofProjectedArea > 0
                ? input.RoofProjectedArea
                : Math.PI * Math.Pow(diametroM / 2.0, 2);

            // Calcula la parte enterrada o embebida del tanque.
            double embebidoMm = CalcularEmbebido(resultado);
            resultado.EmbebidoCalculado = embebidoMm;

            // Completa los distintos bloques de resultados AWWA.
            CalcularAxial(input, resultado, anillosTopDown, diametroMm, radioMm, roofAreaM2, embebidoMm);
            CalcularViento(input, resultado, anillosTopDown, diametroMm, radioMm, diametroM, roofAreaM2, embebidoMm);
            CalcularSismo(input, resultado, anillosTopDown, diametroMm, radioMm, diametroM, alturaLiquidoMm, embebidoMm);
            CalcularCombinada(input, resultado, anillosTopDown, diametroM, alturaLiquidoMm);
        }

        // Calcula el estado axial de cada anillo teniendo en cuenta peso propio y cubierta.
        private void CalcularAxial(
            CalculoTanqueInputModel input,
            ResultadoCalculoModel resultado,
            List<ResultadoAnilloModel> anillosTopDown,
            double diametroMm,
            double radioMm,
            double roofAreaM2,
            double embebidoMm)
        {
            // Convierte las cargas de cubierta a una carga lineal por metro de virola.
            double cargaCubiertaPorMetro = ((input.RoofDeadLoad + Math.Max(input.RoofLiveLoad, input.RoofSnowLoad))
                                            * roofAreaM2)
                                           / (Math.PI * diametroMm) / 1000.0;

            double axialLoad = 0.0;

            for (int i = 0; i < anillosTopDown.Count; i++)
            {
                ResultadoAnilloModel anillo = anillosTopDown[i];
                double alturaMm = ObtenerAlturaAnilloMm(anillo);

                // En el anillo inferior descuenta la parte embebida.
                double alturaEfectivaMm = i == anillosTopDown.Count - 1
                    ? Math.Max(0.0, alturaMm - embebidoMm)
                    : alturaMm;

                // Peso lineal del anillo.
                double pesoAnillo = DensidadAceroAwwa * (anillo.EspesorSeleccionado / 1000.0) * (alturaEfectivaMm / 1000.0);

                // El anillo superior recibe además la carga de cubierta.
                if (i == 0)
                    axialLoad = pesoAnillo + cargaCubiertaPorMetro;
                else
                    axialLoad += pesoAnillo;

                int nTornillosHorizontal = anillo.NumeroTornillosHorizontalesCalculo > 0
                    ? anillo.NumeroTornillosHorizontalesCalculo
                    : Math.Max(1, anillo.NumeroTornillosHorizontales);

                // Calcula tensión axial y comprobaciones de bearing y cortante en tornillos.
                double axialStress = anillo.EspesorSeleccionado > 0 ? axialLoad / anillo.EspesorSeleccionado : 0.0;
                double allowableAxial = CalcularAxialAdmisible(anillo.EspesorSeleccionado, radioMm);
                double bearing = CalcularBearingAxial(axialLoad, diametroMm, input.ChapasPorAnillo, nTornillosHorizontal, anillo.DiametroTornilloAplicado, anillo.EspesorSeleccionado);
                double allowableBearing = 1.35 * anillo.FyPlancha;
                double shear = CalcularCortanteAxial(axialLoad, diametroMm, input.ChapasPorAnillo, nTornillosHorizontal, anillo.DiametroTornilloAplicado);
                double allowableShear = 0.25 * EstimarFuTornillo(anillo);

                // Guarda los resultados del anillo.
                anillo.AxialLoad = axialLoad;
                anillo.AxialStress = axialStress;
                anillo.AllowableAxialStress = allowableAxial;
                anillo.AxialHoleBearingStress = bearing;
                anillo.AxialAllowableBearingStress = allowableBearing;
                anillo.AxialBoltShearStress = shear;
                anillo.AxialAllowableShearStress = allowableShear;
                anillo.AxialEsValido =
                    axialStress * CoefSeguridadAwwa <= allowableAxial &&
                    bearing * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= allowableBearing &&
                    shear * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= allowableShear;
            }
        }

        // Calcula los efectos del viento sobre el tanque y sobre cada anillo.
        private void CalcularViento(
            CalculoTanqueInputModel input,
            ResultadoCalculoModel resultado,
            List<ResultadoAnilloModel> anillosTopDown,
            double diametroMm,
            double radioMm,
            double diametroM,
            double roofAreaM2,
            double embebidoMm)
        {
            double alturaTotalM = (input.AlturaTotal > 0 ? input.AlturaTotal : resultado.AlturaTotal) / 1000.0;
            double velocidadViento = input.VelocidadViento;

            // Calcula la presión dinámica del viento según exposición y altura.
            double kz = ObtenerKz(input.ClaseExposicion, Math.Max(4.6, alturaTotalM));
            double qz = Math.Max((0.613 * kz * 1.0 * velocidadViento * velocidadViento) / 1000.0, 1.44);
            double windPressureTanque = qz * CoefFuerzaVientoVirola;
            double windPressureTecho = qz * CoefFuerzaVientoTecho;
            double centroideTechoMm = input.RoofCentroid;

            // Corta y momento globales por viento.
            double windShearForceTanque = windPressureTanque * diametroM * alturaTotalM;
            double windShearForceTecho = windPressureTecho * roofAreaM2;

            resultado.WindShearForceAtBase = windShearForceTanque + windShearForceTecho;
            resultado.WindShear = diametroM > 0 ? resultado.WindShearForceAtBase / (diametroM / 2.0 * Math.PI) : 0.0;
            resultado.WindOverturningMoment = windShearForceTanque * (alturaTotalM / 2.0) + windShearForceTecho * (alturaTotalM + centroideTechoMm / 1000.0);
            resultado.MaximumAxialLoadDueToWindOTM = diametroM > 0 ? resultado.WindOverturningMoment / (diametroM * diametroM * Math.PI / 4.0) : 0.0;

            // Cálculo específico del levantamiento en cubierta.
            double roofWindOTM = windShearForceTecho * (alturaTotalM + centroideTechoMm / 1000.0);
            double maxRoofAxial = diametroM > 0 ? roofWindOTM / (diametroM * diametroM * Math.PI / 4.0) : 0.0;
            resultado.RoofWindUplift = (maxRoofAxial * (diametroM / 2.0) * Math.PI) / 2.0;

            double axialDeadLoad = 0.0;
            double alturaEfectivaMm = 0.0;

            for (int i = 0; i < anillosTopDown.Count; i++)
            {
                ResultadoAnilloModel anillo = anillosTopDown[i];
                double alturaMm = ObtenerAlturaAnilloMm(anillo);
                double alturaEfectivaAnilloMm = i == anillosTopDown.Count - 1
                    ? Math.Max(0.0, alturaMm - embebidoMm)
                    : alturaMm;

                double pesoAnillo = DensidadAceroAwwa * (anillo.EspesorSeleccionado / 1000.0) * (alturaEfectivaAnilloMm / 1000.0);
                alturaEfectivaMm += alturaEfectivaAnilloMm;

                // En el primer anillo añade la carga muerta de cubierta.
                if (i == 0)
                {
                    axialDeadLoad = pesoAnillo + ((input.RoofDeadLoad * roofAreaM2) / (Math.PI * diametroMm)) / 1000.0;
                }
                else
                {
                    axialDeadLoad += pesoAnillo;
                }

                // Momento por viento del techo y de la virola hasta la altura del anillo.
                double windOverturningMomentTecho = windShearForceTecho * (alturaEfectivaMm / 1000.0 + centroideTechoMm / 1000.0);
                double windOverturningMomentTanque = windPressureTanque * (diametroM * (alturaEfectivaMm / 1000.0)) * (alturaEfectivaMm / 2000.0);
                double axialLoadDueToWind = diametroM > 0 ? (windOverturningMomentTecho + windOverturningMomentTanque) / (diametroM * diametroM * Math.PI / 4.0) : 0.0;
                double axialLoad = axialDeadLoad + axialLoadDueToWind;

                int nTornillosHorizontal = anillo.NumeroTornillosHorizontalesCalculo > 0
                    ? anillo.NumeroTornillosHorizontalesCalculo
                    : Math.Max(1, anillo.NumeroTornillosHorizontales);

                // Guarda resultados de viento del anillo.
                anillo.WindAlturaEfectiva = alturaEfectivaMm;
                anillo.WindAxialDeadLoad = axialDeadLoad;
                anillo.WindAxialLoad = axialLoad;
                anillo.WindAxialStress = anillo.EspesorSeleccionado > 0 ? axialLoad / anillo.EspesorSeleccionado : 0.0;
                anillo.WindAllowableAxialStress = CalcularAxialAdmisible(anillo.EspesorSeleccionado, radioMm) * 4.0 / 3.0;
                anillo.WindHoleBearingStress = CalcularBearingAxial(axialLoad, diametroMm, input.ChapasPorAnillo, nTornillosHorizontal, anillo.DiametroTornilloAplicado, anillo.EspesorSeleccionado);
                anillo.WindAllowableBearingStress = 1.35 * anillo.FyPlancha * 4.0 / 3.0;
                anillo.WindBoltShearStress = CalcularCortanteAxial(axialLoad, diametroMm, input.ChapasPorAnillo, Math.Max(1, anillo.NumeroTornillosHorizontales), anillo.DiametroTornilloAplicado);
                anillo.WindAllowableShearStress = 0.25 * EstimarFuTornillo(anillo) * 4.0 / 3.0;
                anillo.WindEsValido =
                    anillo.WindAxialStress * CoefSeguridadAwwa <= anillo.WindAllowableAxialStress &&
                    anillo.WindHoleBearingStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.WindAllowableBearingStress &&
                    anillo.WindBoltShearStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.WindAllowableShearStress;
            }
        }

        // Calcula los efectos sísmicos globales y por anillo según AWWA.
        private void CalcularSismo(
            CalculoTanqueInputModel input,
            ResultadoCalculoModel resultado,
            List<ResultadoAnilloModel> anillosTopDown,
            double diametroMm,
            double radioMm,
            double diametroM,
            double alturaLiquidoMm,
            double embebidoMm)
        {
            // Si faltan datos sísmicos mínimos, no calcula este bloque.
            if (input.Ss <= 0 || input.S1 <= 0 || string.IsNullOrWhiteSpace(input.SiteClass) || string.IsNullOrWhiteSpace(input.SeismicUseGroup))
                return;

            double importancia = ObtenerImportanceFactor(input.SeismicUseGroup);
            double fa = ObtenerShortPeriodSiteCoefficientFa(input.Ss, input.SiteClass);
            double fv = ObtenerLongPeriodSiteCoefficientFv(input.S1, input.SiteClass);

            double sms = fa * input.Ss / 100.0;
            double sm1 = fv * input.S1 / 100.0;
            double sds = sms * 2.0 / 3.0;
            double sd1 = sm1 * 2.0 / 3.0;
            double sai = sds;
            double hLiquidoM = alturaLiquidoMm / 1000.0;

            if (hLiquidoM <= 0 || diametroM <= 0)
                return;

            // Periodo convectivo del líquido.
            double tc = 2.0 * Math.PI * Math.Sqrt(diametroM / (3.68 * 9.816 * Math.Tanh(3.68 * hLiquidoM / diametroM)));
            double sac = tc <= input.TL ? 1.5 * sd1 / tc : 1.5 * sd1 * input.TL / (tc * tc);
            double ri = 3.0;
            double rc = 1.5;
            double ai = Math.Max(sai * importancia / (1.4 * ri), 0.36 * input.S1 / 100.0 * importancia / ri);
            double ac = sac * importancia / (1.4 * rc);
            double av = 0.14 * sds;

            resultado.AwwaAi = ai;
            resultado.AwwaAc = ac;
            resultado.AwwaAv = av;

            // Peso del líquido contenido en el tanque.
            resultado.WeightOfContents = hLiquidoM * Math.PI * diametroM * diametroM * input.DensidadLiquido * 9.81 / 4.0;

            double wi;
            double xi;
            double wc;
            double xc;
            double ximf;
            double xcmf;

            // Obtiene masas y centroides impulsivos y convectivos.
            if (diametroMm / alturaLiquidoMm >= 4.0 / 3.0)
            {
                wi = (Math.Tanh(0.866 * diametroMm / alturaLiquidoMm) / (0.866 * diametroMm / alturaLiquidoMm)) * resultado.WeightOfContents;
                xi = 0.375 * hLiquidoM;
                ximf = 0.375 * (1.0 - 4.0 / 3.0 * ((0.866 * diametroMm / alturaLiquidoMm) / Math.Tanh(0.866 * diametroMm / alturaLiquidoMm) - 1.0));
            }
            else
            {
                wi = (1.0 - 0.218 * diametroMm / alturaLiquidoMm) * resultado.WeightOfContents;
                xi = (0.5 - 0.094 * diametroMm / alturaLiquidoMm) * hLiquidoM;
                ximf = (0.5 + 0.06 * diametroMm / alturaLiquidoMm) * hLiquidoM;
            }

            wc = 0.230 * diametroMm / alturaLiquidoMm * Math.Tanh(3.67 * alturaLiquidoMm / diametroMm) * resultado.WeightOfContents;
            xc = (1.0 - (Math.Cosh(3.67 * alturaLiquidoMm / diametroMm) - 1.0) / (3.67 * alturaLiquidoMm / diametroMm * Math.Sinh(3.67 * alturaLiquidoMm / diametroMm))) * hLiquidoM;
            xcmf = (1.0 - (Math.Cosh(3.67 * alturaLiquidoMm / diametroMm) - 1.937) / (3.67 * alturaLiquidoMm / diametroMm * Math.Sinh(3.67 * alturaLiquidoMm / diametroMm))) * hLiquidoM;

            // Cargas globales del tanque.
            resultado.TankShellDeadLoad = anillosTopDown.Sum(a => ObtenerPesoAnillo(a, 0.0, false, diametroM));
            resultado.RoofDeadLoad = input.RoofDeadLoad * Math.Pow(diametroM, 2) * Math.PI / 4.0;

            double xsTanque = CalcularCentroMasasTanque(anillosTopDown, alturaTotalCarcasaMm: alturaLiquidoMm + embebidoMm, diametroM: diametroM);
            double ht = (alturaLiquidoMm + embebidoMm) / 1000.0;

            // Resultados globales sísmicos.
            resultado.SeismicShearA = Math.Sqrt(Math.Pow(ai * (resultado.TankShellDeadLoad + resultado.RoofDeadLoad + wi), 2) + Math.Pow(ac * wc, 2));
            resultado.SeismicShearB = diametroM > 0 ? resultado.SeismicShearA / (diametroM / 2.0 * Math.PI) : 0.0;
            resultado.SeismicOTMAtBaseOfShell = Math.Sqrt(Math.Pow(ai * (resultado.TankShellDeadLoad * xsTanque + resultado.RoofDeadLoad * ht + wi * xi), 2) + Math.Pow(ac * wc * xc, 2));
            resultado.MaximumAxialLoadDueToSeismicOTMatBaseOfShell = diametroM > 0 ? resultado.SeismicOTMAtBaseOfShell / (diametroM * diametroM * Math.PI / 4.0) : 0.0;
            resultado.SeismicOTMatTopOfFoundation = Math.Sqrt(Math.Pow(ai * (resultado.TankShellDeadLoad * xsTanque + resultado.RoofDeadLoad * ht + wi * ximf), 2) + Math.Pow(ac * wc * xcmf, 2));

            double axialDeadLoad = 0.0;
            double alturaEfectivaMm = 0.0;

            for (int i = 0; i < anillosTopDown.Count; i++)
            {
                ResultadoAnilloModel anillo = anillosTopDown[i];
                double alturaMm = ObtenerAlturaAnilloMm(anillo);
                double alturaEfectivaAnilloMm = i == anillosTopDown.Count - 1
                    ? Math.Max(0.0, alturaMm - embebidoMm)
                    : alturaMm;

                double pesoAnillo = DensidadAceroAwwa * (anillo.EspesorSeleccionado / 1000.0) * (alturaEfectivaAnilloMm / 1000.0);
                alturaEfectivaMm += alturaEfectivaAnilloMm;

                // En el primer anillo añade la carga de cubierta.
                if (i == 0)
                    axialDeadLoad = pesoAnillo + ((input.RoofDeadLoad * Math.PI * Math.Pow(diametroMm / 2.0, 2)) / (diametroMm * Math.PI)) / 1000.0;
                else
                    axialDeadLoad += pesoAnillo;

                // Calcula centro de masas y momento sísmico acumulado hasta el anillo.
                double centroMasasAnillos = CalcularCentroMasasParcial(anillosTopDown, i, alturaEfectivaMm, diametroM);
                double pesoCarcasaAnillos = axialDeadLoad - resultado.RoofDeadLoad / (diametroM * Math.PI);
                double centroMasasTecho = (alturaEfectivaMm + input.RoofCentroid) / 1000.0;
                double alturaImpulsivaAnillo = Math.Max(0.0, xi + alturaEfectivaMm / 1000.0 - alturaLiquidoMm / 1000.0);
                double alturaConvectivaAnillo = Math.Max(0.0, xc + alturaEfectivaMm / 1000.0 - alturaLiquidoMm / 1000.0);

                double seismicMoment = Math.Sqrt(
                    Math.Pow(ai * (pesoCarcasaAnillos * centroMasasAnillos + resultado.RoofDeadLoad * centroMasasTecho + wi * alturaImpulsivaAnillo), 2) +
                    Math.Pow(ac * wc * alturaConvectivaAnillo, 2));

                double axialLoad = diametroM > 0 ? axialDeadLoad + seismicMoment / (diametroM * diametroM * Math.PI / 4.0) : axialDeadLoad;

                int nTornillosHorizontal = anillo.NumeroTornillosHorizontalesCalculo > 0
                    ? anillo.NumeroTornillosHorizontalesCalculo
                    : Math.Max(1, anillo.NumeroTornillosHorizontales);

                // Guarda resultados sísmicos del anillo.
                anillo.SeismicAlturaEfectiva = alturaEfectivaMm;
                anillo.SeismicAxialDeadLoad = axialDeadLoad;
                anillo.SeismicAxialLoad = axialLoad;
                anillo.SeismicAxialStress = anillo.EspesorSeleccionado > 0 ? axialLoad / anillo.EspesorSeleccionado : 0.0;
                anillo.SeismicAllowableAxialStress = CalcularAxialAdmisible(anillo.EspesorSeleccionado, radioMm) * 4.0 / 3.0;
                anillo.SeismicHoleBearingStress = CalcularBearingAxial(axialLoad, diametroMm, input.ChapasPorAnillo, nTornillosHorizontal, anillo.DiametroTornilloAplicado, anillo.EspesorSeleccionado);
                anillo.SeismicAllowableBearingStress = 1.35 * anillo.FyPlancha * 4.0 / 3.0;
                anillo.SeismicBoltShearStress = CalcularCortanteAxial(axialLoad, diametroMm, input.ChapasPorAnillo, Math.Max(1, anillo.NumeroTornillosHorizontales), anillo.DiametroTornilloAplicado);
                anillo.SeismicAllowableShearStress = 0.25 * EstimarFuTornillo(anillo) * 4.0 / 3.0;
                anillo.SeismicEsValido =
                    anillo.SeismicAxialStress * CoefSeguridadAwwa <= anillo.SeismicAllowableAxialStress &&
                    anillo.SeismicHoleBearingStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.SeismicAllowableBearingStress &&
                    anillo.SeismicBoltShearStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.SeismicAllowableShearStress;
            }

            // Calcula oleaje sísmico y borde libre mínimo requerido.
            double af;
            double k = 1.5;
            if (input.SeismicUseGroup != "III")
                af = tc <= 4.0 ? k * sd1 * importancia / tc : 4.0 * k * sd1 * importancia / (tc * tc);
            else
                af = tc <= input.TL ? k * sd1 / tc : k * sd1 * input.TL / (tc * tc);

            resultado.SloshingWave = 0.5 * diametroM * af;
            switch (input.SeismicUseGroup)
            {
                case "II":
                    resultado.MinimumFreeboardRequirements = sds < 0.33 ? 0.0 : 0.7 * resultado.SloshingWave;
                    break;
                case "III":
                    resultado.MinimumFreeboardRequirements = resultado.SloshingWave;
                    break;
                default:
                    resultado.MinimumFreeboardRequirements = 0.0;
                    break;
            }

            resultado.FreeboardIsOk = ((input.BordeLibre + 25.0) / 1000.0) >= resultado.MinimumFreeboardRequirements;
        }

        // Calcula la combinación de tensiones hidrostáticas e hidrodinámicas.
        private void CalcularCombinada(
            CalculoTanqueInputModel input,
            ResultadoCalculoModel resultado,
            List<ResultadoAnilloModel> anillosTopDown,
            double diametroM,
            double alturaLiquidoMm)
        {
            if (resultado.AwwaAc <= 0 && resultado.AwwaAi <= 0 && resultado.AwwaAv <= 0)
                return;

            double h = alturaLiquidoMm / 1000.0;

            foreach (ResultadoAnilloModel anillo in anillosTopDown)
            {
                double y = anillo.Head;
                double nh = anillo.HydrostaticHoopLoad;

                // Carga convectiva.
                double nc = diametroM > 0
                    ? (1.850 * resultado.AwwaAc * input.DensidadLiquido * Math.Pow(diametroM, 2) * Math.Cosh(3.68 * (h - y) / diametroM)) / Math.Cosh(3.68 * h / diametroM)
                    : 0.0;

                double ni;

                // Carga impulsiva.
                if (diametroM / h >= 4.0 / 3.0)
                {
                    ni = 8.480 * resultado.AwwaAi * input.DensidadLiquido * diametroM * h * (y / h - 0.5 * Math.Pow(y / h, 2)) * Math.Tanh(0.866 * diametroM / h);
                }
                else
                {
                    if (y < 0.75 * diametroM)
                        ni = 5.220 * resultado.AwwaAi * input.DensidadLiquido * Math.Pow(diametroM, 2) * (y / (0.75 * diametroM) - 0.5 * Math.Pow(y / (0.75 * diametroM), 2));
                    else
                        ni = 2.620 * resultado.AwwaAi * input.DensidadLiquido * Math.Pow(diametroM, 2);
                }

                // Suma la componente hidrostática y las sísmicas.
                double total = nh + Math.Sqrt(Math.Pow(ni, 2) + Math.Pow(nc, 2) + Math.Pow(nh * resultado.AwwaAv, 2));
                anillo.CombinedTotalHoopLoad = total;
                anillo.CombinedNetTensileStress = (total * anillo.PasoS) / (anillo.EspesorSeleccionado * (anillo.PasoS - anillo.DiametroAgujero));
                anillo.CombinedAllowableTensileStress = Math.Min(
                    0.6 * anillo.FyPlancha * (1.0 - 0.9 * anillo.RelacionR + 3.0 * anillo.RelacionR * anillo.DiametroTornilloAplicado / anillo.PasoS),
                    0.4 * anillo.FuPlancha) * 4.0 / 3.0;
                anillo.CombinedHoleBearingStress = (total * anillo.PasoS * anillo.RelacionR) / (anillo.DiametroTornilloAplicado * anillo.EspesorSeleccionado);
                anillo.CombinedAllowableBearingStress = 1.35 * anillo.FyPlancha * 4.0 / 3.0;
                anillo.CombinedBoltShearStress = (total * anillo.PasoS * anillo.RelacionR) / (Math.PI * anillo.DiametroTornilloAplicado * anillo.DiametroTornilloAplicado / 4.0);
                anillo.CombinedAllowableShearStress = 0.25 * EstimarFuTornillo(anillo) * 4.0 / 3.0;
                anillo.CombinedEsValido =
                    anillo.CombinedNetTensileStress * CoefSeguridadAwwa <= anillo.CombinedAllowableTensileStress &&
                    anillo.CombinedHoleBearingStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.CombinedAllowableBearingStress &&
                    anillo.CombinedBoltShearStress * CoefSeguridadAwwa * CoefSeguridadTornillosAwwa <= anillo.CombinedAllowableShearStress;
            }
        }

        // Calcula la parte embebida del tanque a partir del starter ring.
        private static double CalcularEmbebido(ResultadoCalculoModel resultado)
        {
            if (!resultado.TieneStarterRing || resultado.AlturaStarterRing <= 0)
                return 0.0;

            double f = ParsePrimerDouble(resultado.FStarterRingTexto);
            if (f <= 0)
                return 0.0;

            return Math.Max(0.0, resultado.AlturaStarterRing - resultado.DistanciaFStarterRing * f);
        }

        // Intenta leer el primer valor numérico de un texto.
        private static double ParsePrimerDouble(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return 0.0;

            string token = texto.Split(',').FirstOrDefault()?.Trim() ?? string.Empty;
            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
                return v;
            if (double.TryParse(token, NumberStyles.Any, new CultureInfo("es-ES"), out v))
                return v;
            return 0.0;
        }

        // Comprueba si una normativa corresponde a AWWA.
        private static bool EsAwwa(string normativa)
        {
            return !string.IsNullOrWhiteSpace(normativa) &&
                   normativa.Trim().ToUpperInvariant().Contains("AWWA");
        }

        // Devuelve la altura real del anillo.
        private static double ObtenerAlturaAnilloMm(ResultadoAnilloModel anillo)
        {
            double altura = anillo.AlturaSuperior - anillo.AlturaInferior;
            return altura > 0 ? altura : 0.0;
        }

        // Calcula la tensión axial admisible según espesor y radio.
        private static double CalcularAxialAdmisible(double espesorMm, double radioMm)
        {
            if (espesorMm <= 0 || radioMm <= 0)
                return 0.0;

            double factor = 100.0 * espesorMm / radioMm;
            return 103.0 * (2.0 / 3.0) * factor * (2.0 - (2.0 / 3.0) * factor);
        }

        // Calcula la tensión de aplastamiento en la unión.
        private static double CalcularBearingAxial(double axialLoad, double diametroMm, int numeroPlanchas, int nTornillos, double diametroTornillo, double espesorMm)
        {
            if (numeroPlanchas <= 0 || nTornillos <= 0 || diametroTornillo <= 0 || espesorMm <= 0)
                return 0.0;

            return (axialLoad * diametroMm * Math.PI) / (numeroPlanchas * nTornillos) / (diametroTornillo * espesorMm);
        }

        // Calcula la tensión cortante en tornillos.
        private static double CalcularCortanteAxial(double axialLoad, double diametroMm, int numeroPlanchas, int nTornillos, double diametroTornillo)
        {
            if (numeroPlanchas <= 0 || nTornillos <= 0 || diametroTornillo <= 0)
                return 0.0;

            return ((axialLoad * 1000.0 * (diametroMm * Math.PI)) / (nTornillos * numeroPlanchas) / (diametroTornillo * diametroTornillo * Math.PI / 4.0)) / 1000.0;
        }

        // Estima la resistencia última del tornillo según su calidad.
        private static double EstimarFuTornillo(ResultadoAnilloModel anillo)
        {
            string calidad = anillo.TornilloAplicado?.Trim().ToUpperInvariant() ?? string.Empty;
            if (calidad.Contains("10.9"))
                return 1000.0;
            if (calidad.Contains("8.8"))
                return 800.0;
            if (calidad.Contains("A2-70") || calidad.Contains("70"))
                return 700.0;
            return 800.0;
        }

        // Devuelve el factor de importancia sísmica según el grupo de uso.
        private static double ObtenerImportanceFactor(string grupo)
        {
            switch ((grupo ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "III": return 1.5;
                case "II": return 1.25;
                default: return 1.0;
            }
        }

        // Interpolación lineal entre dos puntos.
        private static double Interpolar(double x, double x1, double x2, double y1, double y2)
        {
            if (Math.Abs(x2 - x1) < 0.000001)
                return y1;
            return y1 + (x - x1) * (y2 - y1) / (x2 - x1);
        }

        // Devuelve el coeficiente Fa de periodo corto según AWWA.
        private static double ObtenerShortPeriodSiteCoefficientFa(double ss, string siteClass)
        {
            ss /= 100.0;
            double[] v;
            switch ((siteClass ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": v = new[] { 0.8, 0.8, 0.8, 0.8, 0.8 }; break;
                case "B": v = new[] { 1.0, 1.0, 1.0, 1.0, 1.0 }; break;
                case "C": v = new[] { 1.2, 1.2, 1.1, 1.0, 1.0 }; break;
                case "D": v = new[] { 1.6, 1.4, 1.2, 1.1, 1.0 }; break;
                case "E": v = new[] { 2.5, 1.7, 1.2, 0.9, 0.9 }; break;
                default: return 0.0;
            }

            if (ss <= 0.25) return v[0];
            if (ss >= 1.25) return v[4];
            if (ss < 0.5) return Interpolar(ss, 0.25, 0.5, v[0], v[1]);
            if (ss < 0.75) return Interpolar(ss, 0.5, 0.75, v[1], v[2]);
            if (ss < 1.0) return Interpolar(ss, 0.75, 1.0, v[2], v[3]);
            return Interpolar(ss, 1.0, 1.25, v[3], v[4]);
        }

        // Devuelve el coeficiente Fv de periodo largo según AWWA.
        private static double ObtenerLongPeriodSiteCoefficientFv(double s1, string siteClass)
        {
            s1 /= 100.0;
            double[] v;
            switch ((siteClass ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": v = new[] { 0.8, 0.8, 0.8, 0.8, 0.8 }; break;
                case "B": v = new[] { 1.0, 1.0, 1.0, 1.0, 1.0 }; break;
                case "C": v = new[] { 1.7, 1.6, 1.5, 1.4, 1.3 }; break;
                case "D": v = new[] { 2.4, 2.0, 1.8, 1.6, 1.5 }; break;
                case "E": v = new[] { 3.5, 3.2, 2.8, 2.4, 2.4 }; break;
                default: return 0.0;
            }

            if (s1 <= 0.1) return v[0];
            if (s1 >= 0.5) return v[4];
            if (s1 < 0.2) return Interpolar(s1, 0.1, 0.2, v[0], v[1]);
            if (s1 < 0.3) return Interpolar(s1, 0.2, 0.3, v[1], v[2]);
            if (s1 < 0.4) return Interpolar(s1, 0.3, 0.4, v[2], v[3]);
            return Interpolar(s1, 0.4, 0.5, v[3], v[4]);
        }

        // Calcula el coeficiente Kz de exposición al viento.
        private static double ObtenerKz(string exposicion, double alturaM)
        {
            string exp = (exposicion ?? string.Empty).Trim().ToUpperInvariant();
            double alpha;
            double zg;
            switch (exp)
            {
                case "D":
                    alpha = 11.5;
                    zg = 213.0;
                    break;
                case "C":
                    alpha = 9.5;
                    zg = 274.0;
                    break;
                default:
                    alpha = 7.0;
                    zg = 366.0;
                    break;
            }

            double z = Math.Max(4.6, alturaM);
            return 2.01 * Math.Pow(z / zg, 2.0 / alpha);
        }

        // Calcula el peso de un anillo.
        private static double ObtenerPesoAnillo(ResultadoAnilloModel anillo, double embebidoMm, bool esInferior, double diametroM)
        {
            double alturaMm = ObtenerAlturaAnilloMm(anillo);
            double alturaEfectivaMm = esInferior ? Math.Max(0.0, alturaMm - embebidoMm) : alturaMm;
            double radioM = diametroM / 2.0;
            return alturaEfectivaMm / 1000.0 * anillo.EspesorSeleccionado / 1000.0 * 2.0 * radioM * Math.PI * DensidadAceroAwwa;
        }

        // Calcula el centro de masas de toda la carcasa.
        private static double CalcularCentroMasasTanque(List<ResultadoAnilloModel> anillosTopDown, double alturaTotalCarcasaMm, double diametroM)
        {
            double sumMH = 0.0;
            double sumM = 0.0;
            double alturaSuperiorMm = 0.0;
            double radioM = diametroM / 2.0;

            foreach (ResultadoAnilloModel anillo in anillosTopDown)
            {
                double alturaMm = ObtenerAlturaAnilloMm(anillo);
                double masa = alturaMm / 1000.0 * anillo.EspesorSeleccionado / 1000.0 * 2.0 * radioM * Math.PI * DensidadAceroAwwa;
                sumM += masa;
                sumMH += masa * ((alturaTotalCarcasaMm - alturaSuperiorMm - alturaMm / 2.0) / 1000.0);
                alturaSuperiorMm += alturaMm;
            }

            return sumM > 0 ? sumMH / sumM : 0.0;
        }

        // Calcula el centro de masas acumulado hasta un anillo concreto.
        private static double CalcularCentroMasasParcial(List<ResultadoAnilloModel> anillosTopDown, int hastaIndice, double alturaTotalCarcasaMm, double diametroM)
        {
            double sumMH = 0.0;
            double sumM = 0.0;
            double alturaSuperiorMm = 0.0;
            double radioM = diametroM / 2.0;

            for (int i = 0; i <= hastaIndice && i < anillosTopDown.Count; i++)
            {
                ResultadoAnilloModel anillo = anillosTopDown[i];
                double alturaMm = ObtenerAlturaAnilloMm(anillo);
                double masa = alturaMm / 1000.0 * anillo.EspesorSeleccionado / 1000.0 * 2.0 * radioM * Math.PI * DensidadAceroAwwa;
                sumM += masa;
                sumMH += masa * ((alturaTotalCarcasaMm - alturaSuperiorMm - alturaMm / 2.0) / 1000.0);
                alturaSuperiorMm += alturaMm;
            }

            return sumM > 0 ? sumMH / sumM : 0.0;
        }
    }
}