using System;
using System.Collections.Generic;
using System.Linq;
using TankDesigner.Core.Models.Presupuestos;

namespace TankDesigner.Core.Services
{
    public sealed class PresupuestoInstalacionExcelService
    {
        // === Datos equivalentes a la hoja "Data" del Excel ===

        private const decimal DensidadAceroKgM3 = 7850m;

        // Productividad / tiempos
        private const decimal HorasBasePorPlacaSinGrua = 0.5m;           // Data!J3
        private const decimal HorasPor100KgSinGrua = 1.0m;               // Data!J4
        private const decimal HorasPor100KgConGrua = 0.2m;               // Data!J5
        private const decimal UmbralPesoPanelGruaKg = 150m;              // Data!J7
        private const decimal HorasPorRigidizador = 0.4m;                // Data!J9
        private const decimal HorasSelladoPorMetro = 0.06m;              // Data!J10
        private const decimal HorasEscaleraVerticalPorMetro = 0.5m;      // Data!J11
        private const decimal HorasEscaleraHelicoidalPorMetro = 1.5m;    // Data!J12
        private const decimal HorasConexion25a150 = 0.5m;                // Data!J13
        private const decimal HorasConexion150a300 = 0.7m;               // Data!J14
        private const decimal HorasConexion300a500 = 1.0m;               // Data!J15
        private const decimal HorasConexionMayor500 = 1.2m;              // Data!J16
        private const decimal HorasPorStarterRing = 1.3m;                // Data!J17
        private const decimal HorasAnclajePorMetro = 2.0m;               // Data!J18
        private const decimal HorasBocaHombre = 4.5m;                    // Data!J19
        private const decimal HorasCambioGato = 1.0m;                    // Data!J20

        // Costes hora
        private const decimal CosteHoraOperarioNacional = 32m;           // Data!J21
        private const decimal CosteHoraIngenieroNacional = 40m;          // Data!J22
        private const decimal CosteHoraSeguridadNacional = 45m;          // Data!J23
        private const decimal CosteHoraOperarioInternacional = 45m;      // Data!J24
        private const decimal CosteHoraIngenieroInternacional = 50m;     // Data!J25
        private const decimal CosteHoraSeguridadInternacional = 55m;     // Data!J26

        // Costes adicionales
        private const decimal CosteVueloEuropa = 500m;                   // Data!J72
        private const decimal CosteVueloInternacional = 1000m;           // Data!J73

        // Techo
        private const decimal HorasTechoEstructuraConico = 0.4m;         // Data!J44
        private const decimal HorasTechoPanelesConico = 0.8m;            // Data!K44
        private const decimal HorasTechoEstructuraPlano = 0.2m;          // Data!J45
        private const decimal HorasTechoPanelesPlano = 0.7m;             // Data!K45
        private const decimal HorasTechoEstructuraDomo = 1.0m;           // Data!J46
        private const decimal HorasTechoPanelesDomo = 0.8m;              // Data!K46

        // Escaleras fabricación
        private const decimal CosteEscaleraVerticalPorMetro = 250m;      // Data!J50 = 2500/10
        private const decimal CosteEscaleraHelicoidalPorMetro = 980m;    // Data!J51

        public PresupuestoInstalacionResultadoModel Calcular(PresupuestoInstalacionInputModel input)
        {
            input.Validar();

            var resultado = new PresupuestoInstalacionResultadoModel();

            decimal alturaPanel = ObtenerAlturaPanel(input.Fabricante);
            decimal longitudPanel = ObtenerLongitudPanel(input.Fabricante);
            decimal alturaTanque = alturaPanel * input.NumeroAnillos;
            decimal perimetro = (decimal)Math.PI * input.DiametroMetros;
            decimal areaTecho = (decimal)Math.PI * input.DiametroMetros * input.DiametroMetros / 4m;

            resultado.AlturaPanelMetros = Round2(alturaPanel);
            resultado.LongitudPanelMetros = Round2(longitudPanel);
            resultado.AlturaTanqueMetros = Round2(alturaTanque);
            resultado.PerimetroTanqueMetros = Round2(perimetro);
            resultado.AreaTechoM2 = Round2(areaTecho);

            // 1) Horas por placas según espesores reales del cálculo
            var conteoEspesores = input.EspesoresAnillosMm
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count() * input.NumeroPlacasPorAnillo);

            decimal horasMontajePlacas = 0m;
            decimal horasCamionGrua = 0m;

            foreach (var item in conteoEspesores.OrderBy(x => x.Key))
            {
                decimal espesorMm = item.Key;
                int numeroPlacas = item.Value;

                decimal pesoPanelKg = alturaPanel * longitudPanel * (espesorMm / 1000m) * DensidadAceroKgM3;

                decimal horasPanelSinGrua = HorasBasePorPlacaSinGrua + (HorasPor100KgSinGrua * pesoPanelKg / 100m);
                decimal horasPanelConGrua = HorasBasePorPlacaSinGrua + (HorasPor100KgConGrua * pesoPanelKg / 100m);

                bool requiereGrua = pesoPanelKg >= UmbralPesoPanelGruaKg;
                decimal horasUnitarias = requiereGrua ? horasPanelConGrua : horasPanelSinGrua;
                decimal horasTotalesEspesor = horasUnitarias * numeroPlacas;

                horasMontajePlacas += horasTotalesEspesor;

                if (requiereGrua)
                {
                    horasCamionGrua += horasTotalesEspesor;
                }
            }

            // 2) Resto de horas según Excel
            decimal horasCambiosGato = input.NumeroAnillos * input.NumeroPlacasPorAnillo * HorasCambioGato;

            decimal horasEscaleras = input.TipoEscalera switch
            {
                TipoEscaleraPresupuesto.SinEscalera => 0m,
                TipoEscaleraPresupuesto.Vertical => input.NumeroEscaleras * alturaTanque * HorasEscaleraVerticalPorMetro,
                TipoEscaleraPresupuesto.Helicoidal => input.NumeroEscaleras * alturaTanque * HorasEscaleraHelicoidalPorMetro,
                _ => 0m
            };

            decimal horasConexiones =
                (input.ConexionesDn25a150 * HorasConexion25a150) +
                (input.ConexionesDn150a300 * HorasConexion150a300) +
                (input.ConexionesDn300a500 * HorasConexion300a500) +
                (input.ConexionesMayor500 * HorasConexionMayor500) +
                (input.NumeroBocasHombre * HorasBocaHombre);

            decimal horasRigidizadores =
                (decimal)Math.Ceiling((double)input.NumeroPlacasPorAnillo / input.NumeroAnillos) *
                input.NumeroPlacasPorAnillo *
                HorasPorRigidizador;

            decimal horasAnclaje = perimetro * HorasAnclajePorMetro;
            decimal horasSellado = perimetro * HorasSelladoPorMetro;

            decimal horasTechoEstructura = input.TipoTecho switch
            {
                TipoTechoPresupuesto.SinTecho => 0m,
                TipoTechoPresupuesto.Conico => areaTecho * HorasTechoEstructuraConico,
                TipoTechoPresupuesto.Plano => areaTecho * HorasTechoEstructuraPlano,
                TipoTechoPresupuesto.DomoGeodesico => areaTecho * HorasTechoEstructuraDomo,
                _ => 0m
            };

            decimal horasTechoPaneles = input.TipoTecho switch
            {
                TipoTechoPresupuesto.SinTecho => 0m,
                TipoTechoPresupuesto.Conico => areaTecho * HorasTechoPanelesConico,
                TipoTechoPresupuesto.Plano => areaTecho * HorasTechoPanelesPlano,
                TipoTechoPresupuesto.DomoGeodesico => areaTecho * HorasTechoPanelesDomo,
                _ => 0m
            };

            decimal horasTotalesTecho = horasTechoEstructura + horasTechoPaneles;
            decimal horasTotalesDeposito =
                horasMontajePlacas +
                horasCambiosGato +
                horasEscaleras +
                horasConexiones +
                horasRigidizadores +
                horasAnclaje +
                horasSellado;

            // Excel X28
            decimal horasDescanso = 0m;
            if (input.TamanoCuadrilla > 0)
            {
                horasDescanso = ((horasTotalesTecho + horasTotalesDeposito) / (5m * input.TamanoCuadrilla))
                               * (input.HorasTrabajoPorDia * 2m);
            }

            // Ajuste por lluvia
            if (input.PorcentajeLluvia > 0m)
            {
                horasDescanso += (horasTotalesTecho + horasTotalesDeposito) * input.PorcentajeLluvia;
            }

            // Excel X29
            decimal horasDesplazamiento = Math.Max(
                (decimal)Math.Ceiling((double)((horasTotalesTecho + horasTotalesDeposito + horasDescanso) / (input.HorasTrabajoPorDia * 60m))) * 2m * input.HorasTrabajoPorDia,
                input.HorasTrabajoPorDia * 2m * input.TamanoCuadrilla);

            // 3) Calendario
            decimal horasEquipoTecho = (decimal)Math.Ceiling((double)(horasTotalesTecho / input.TamanoCuadrilla));
            decimal diasTecho = (decimal)Math.Ceiling((double)(horasEquipoTecho / input.HorasTrabajoPorDia));

            decimal horasEquipoDeposito = (decimal)Math.Ceiling((double)(horasTotalesDeposito / input.TamanoCuadrilla));
            decimal diasDeposito = (decimal)Math.Ceiling((double)(horasEquipoDeposito / input.HorasTrabajoPorDia));

            decimal horasEquipoDescanso = (decimal)Math.Ceiling((double)(horasDescanso / input.TamanoCuadrilla));
            decimal diasDescanso = (decimal)Math.Ceiling((double)(horasEquipoDescanso / input.HorasTrabajoPorDia));

            decimal horasEquipoDesplazamiento = (decimal)Math.Ceiling((double)(horasDesplazamiento / input.TamanoCuadrilla));
            decimal diasDesplazamiento = (decimal)Math.Ceiling((double)(horasEquipoDesplazamiento / input.HorasTrabajoPorDia));

            // Reproduce el Excel: C39 = SUM(C35:C38), pero C37/C38 vienen vacías
            decimal diasTotalesExcel = diasTecho + diasDeposito;

            resultado.Horas = new HorasInstalacionDetalleModel
            {
                HorasMontajePlacas = Round2(horasMontajePlacas),
                HorasCambiosGato = Round2(horasCambiosGato),
                HorasEscaleras = Round2(horasEscaleras),
                HorasConexionesYBocaHombre = Round2(horasConexiones),
                HorasRigidizadores = Round2(horasRigidizadores),
                HorasAnclaje = Round2(horasAnclaje),
                HorasSelladoCimentacionPared = Round2(horasSellado),
                HorasTechoEstructura = Round2(horasTechoEstructura),
                HorasTechoPaneles = Round2(horasTechoPaneles),
                HorasDescanso = Round2(horasDescanso),
                HorasDesplazamiento = Round2(horasDesplazamiento),
                HorasCamionGrua = Round2(horasCamionGrua)
            };

            resultado.Calendario = new CalendarioInstalacionModel
            {
                HorasEquipoTecho = Round2(horasEquipoTecho),
                DiasTecho = Round2(diasTecho),
                HorasEquipoDeposito = Round2(horasEquipoDeposito),
                DiasDeposito = Round2(diasDeposito),
                HorasEquipoDescanso = Round2(horasEquipoDescanso),
                DiasDescanso = Round2(diasDescanso),
                HorasEquipoDesplazamiento = Round2(horasEquipoDesplazamiento),
                DiasDesplazamiento = Round2(diasDesplazamiento),
                DiasTotalesExcel = Round2(diasTotalesExcel)
            };

            // 4) Costes
            decimal costeHoraOperario = ObtenerCosteHoraOperario(input.UbicacionObra);
            decimal costeHoraIngeniero = ObtenerCosteHoraIngeniero(input.UbicacionObra);
            decimal costeHoraSeguridad = ObtenerCosteHoraSeguridad(input.UbicacionObra);

            // Excel C46 / C47
            decimal divisorDias = (diasTecho + diasDeposito) <= 0m ? 1m : (diasTecho + diasDeposito);
            decimal diasRepartidosTecho = Math.Round((diasTotalesExcel / divisorDias) * diasTecho, 0, MidpointRounding.AwayFromZero);
            decimal diasRepartidosDeposito = Math.Round((diasTotalesExcel / divisorDias) * diasDeposito, 0, MidpointRounding.AwayFromZero);

            decimal costeMontajeTecho = diasRepartidosTecho * input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario;
            decimal costeMontajeDeposito = diasRepartidosDeposito * input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario;

            decimal costeSiteManager = input.NumeroSiteManagers * diasTotalesExcel * input.HorasTrabajoPorDia * costeHoraIngeniero;
            decimal costeSeguridad = input.NumeroTecnicosSeguridad * diasTotalesExcel * input.HorasTrabajoPorDia * costeHoraSeguridad;

            decimal numeroVuelos = (horasDesplazamiento + ((input.NumeroSiteManagers + input.NumeroTecnicosSeguridad) * input.HorasTrabajoPorDia * 2m))
                                 / input.HorasTrabajoPorDia;

            decimal costeVueloUnitario = ObtenerCosteVuelo(input.UbicacionObra);
            decimal costeVuelos = numeroVuelos * costeVueloUnitario;

            decimal costeFabricacionEscalera = input.TipoEscalera switch
            {
                TipoEscaleraPresupuesto.SinEscalera => 0m,
                TipoEscaleraPresupuesto.Vertical => input.NumeroEscaleras * alturaTanque * CosteEscaleraVerticalPorMetro,
                TipoEscaleraPresupuesto.Helicoidal => input.NumeroEscaleras * alturaTanque * CosteEscaleraHelicoidalPorMetro,
                _ => 0m
            };

            var partidas = new List<PartidaPresupuestoModel>
            {
                CrearPartida("INST-01", "Montaje techo", diasRepartidosTecho, "día",
                    input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario, costeMontajeTecho),

                CrearPartida("INST-02", "Montaje depósito", diasRepartidosDeposito, "día",
                    input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario, costeMontajeDeposito),

                CrearPartida("INST-03", "Site Manager", input.NumeroSiteManagers * diasTotalesExcel * input.HorasTrabajoPorDia, "h",
                    costeHoraIngeniero, costeSiteManager),

                CrearPartida("INST-04", "Técnico seguridad y salud", input.NumeroTecnicosSeguridad * diasTotalesExcel * input.HorasTrabajoPorDia, "h",
                    costeHoraSeguridad, costeSeguridad),

                CrearPartida("INST-05", "Vuelos", Round2(numeroVuelos), "ud",
                    costeVueloUnitario, costeVuelos),

                CrearPartida("INST-06", "Fabricación escalera", input.NumeroEscaleras * alturaTanque, "m",
                    input.TipoEscalera == TipoEscaleraPresupuesto.Vertical ? CosteEscaleraVerticalPorMetro :
                    input.TipoEscalera == TipoEscaleraPresupuesto.Helicoidal ? CosteEscaleraHelicoidalPorMetro : 0m,
                    costeFabricacionEscalera)
            };

            if (input.CosteTransporteManual > 0m)
            {
                partidas.Add(CrearPartida("INST-07", "Transporte", 1m, "ud", input.CosteTransporteManual, input.CosteTransporteManual));
            }

            resultado.Partidas = partidas
                .Where(x => x.Total > 0m)
                .ToList();

            return resultado;
        }

        private static PartidaPresupuestoModel CrearPartida(
            string codigo,
            string concepto,
            decimal cantidad,
            string unidad,
            decimal precioUnitario,
            decimal total)
        {
            return new PartidaPresupuestoModel
            {
                Codigo = codigo,
                Concepto = concepto,
                Cantidad = Round2(cantidad),
                Unidad = unidad,
                PrecioUnitario = Round2(precioUnitario),
                Total = Round2(total)
            };
        }

        private static decimal ObtenerAlturaPanel(FabricantePresupuesto fabricante)
        {
            return fabricante switch
            {
                FabricantePresupuesto.Balmoral => 1.2m,
                FabricantePresupuesto.Permastore => 1.4m,
                FabricantePresupuesto.DL2 => 1.4m,
                _ => 1.4m
            };
        }

        private static decimal ObtenerLongitudPanel(FabricantePresupuesto fabricante)
        {
            return fabricante switch
            {
                FabricantePresupuesto.Balmoral => 2.45m,
                FabricantePresupuesto.Permastore => 2.68m,
                FabricantePresupuesto.DL2 => 2.68m,
                _ => 2.68m
            };
        }

        private static decimal ObtenerCosteHoraOperario(UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? CosteHoraOperarioNacional
                : CosteHoraOperarioInternacional;
        }

        private static decimal ObtenerCosteHoraIngeniero(UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? CosteHoraIngenieroNacional
                : CosteHoraIngenieroInternacional;
        }

        private static decimal ObtenerCosteHoraSeguridad(UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? CosteHoraSeguridadNacional
                : CosteHoraSeguridadInternacional;
        }

        private static decimal ObtenerCosteVuelo(UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion switch
            {
                UbicacionObraPresupuesto.Nacional => 0m,
                UbicacionObraPresupuesto.Europa => CosteVueloEuropa,
                UbicacionObraPresupuesto.Internacional => CosteVueloInternacional,
                _ => 0m
            };
        }

        private static decimal Round2(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}