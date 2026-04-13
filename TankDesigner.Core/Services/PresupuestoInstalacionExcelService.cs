using System;
using System.Collections.Generic;
using System.Linq;
using TankDesigner.Core.Models.Presupuestos;

namespace TankDesigner.Core.Services
{
    public sealed class PresupuestoInstalacionExcelService
    {
        private readonly JsonCatalogService _jsonCatalogService = new();

        public PresupuestoInstalacionResultadoModel Calcular(PresupuestoInstalacionInputModel input)
        {
            input.Validar();

            var config = _jsonCatalogService.CargarDatosInstalacion(MapearFabricante(input.Fabricante));
            var panel = ObtenerPanelFabricante(config, input.Fabricante);
            var techo = ObtenerTecho(config, input.TipoTecho);
            var escalera = ObtenerEscalera(config, input.TipoEscalera);

            decimal densidadAcero = config.DensidadAcero > 0 ? config.DensidadAcero : 7850m;
            decimal alturaPanel = panel?.AltoPanel > 0 ? panel.AltoPanel : ObtenerAlturaPanelFallback(input.Fabricante);
            decimal longitudPanel = panel?.LargoPanel > 0 ? panel.LargoPanel : ObtenerLongitudPanelFallback(input.Fabricante);

            decimal horasBasePorPlaca = config.Productividad.HorasPorPlacaPersona > 0 ? config.Productividad.HorasPorPlacaPersona : 0.5m;
            decimal horasPor100KgSinGrua = config.Productividad.HorasPor100kgPlacaSinGrua > 0 ? config.Productividad.HorasPor100kgPlacaSinGrua : 1.0m;
            decimal horasPor100KgConGrua = config.Productividad.HorasPor100kgPlacaConGrua > 0 ? config.Productividad.HorasPor100kgPlacaConGrua : 0.2m;
            decimal panelesPorDiaCamionGrua = config.Productividad.PanelesPorDiaCamionGrua > 0 ? config.Productividad.PanelesPorDiaCamionGrua : 0m;
            decimal umbralPesoPanelGrua = config.Productividad.UmbralPesoPanelGrua > 0 ? config.Productividad.UmbralPesoPanelGrua : 150m;
            decimal horasPorRigidizador = config.Productividad.HorasPorRigidizador > 0 ? config.Productividad.HorasPorRigidizador : 0.4m;
            decimal horasSelladoPorMetro = config.Productividad.HorasSelladoMetro > 0 ? config.Productividad.HorasSelladoMetro : 0.06m;
            decimal horasEscaleraVerticalMetro = config.Productividad.HorasEscaleraVerticalMetro > 0 ? config.Productividad.HorasEscaleraVerticalMetro : 0.5m;
            decimal horasEscaleraHelicoidalMetro = config.Productividad.HorasEscaleraHelicoidalMetro > 0 ? config.Productividad.HorasEscaleraHelicoidalMetro : 1.5m;
            decimal horasConexion25a150 = config.Productividad.HorasConexion.DN25_DN150 > 0 ? config.Productividad.HorasConexion.DN25_DN150 : 0.5m;
            decimal horasConexion150a300 = config.Productividad.HorasConexion.DN150_DN300 > 0 ? config.Productividad.HorasConexion.DN150_DN300 : 0.7m;
            decimal horasConexion300a500 = config.Productividad.HorasConexion.DN300_DN500 > 0 ? config.Productividad.HorasConexion.DN300_DN500 : 1.0m;
            decimal horasConexionMayor500 = config.Productividad.HorasConexion.DN500 > 0 ? config.Productividad.HorasConexion.DN500 : 1.2m;
            decimal horasStarterRing = config.Productividad.HorasStarterRing > 0 ? config.Productividad.HorasStarterRing : 0m;
            decimal horasAnclajePorMetro = config.Productividad.HorasAnclajeMetro > 0 ? config.Productividad.HorasAnclajeMetro : 2.0m;
            decimal horasBocaHombre = config.Productividad.HorasBocaHombre > 0 ? config.Productividad.HorasBocaHombre : 4.5m;
            decimal horasCambioGato = config.Productividad.HorasCambioGato > 0 ? config.Productividad.HorasCambioGato : 1.0m;

            decimal costeHoraOperario = ObtenerCosteHoraOperario(config, input.UbicacionObra);
            decimal costeHoraIngeniero = ObtenerCosteHoraIngeniero(config, input.UbicacionObra);
            decimal costeHoraSeguridad = ObtenerCosteHoraSeguridad(config, input.UbicacionObra);
            decimal costeVueloUnitario = ObtenerCosteVuelo(config, input.UbicacionObra);
            decimal costeFabricacionEscaleraMetro = escalera?.PrecioMetro ?? ObtenerPrecioEscaleraFallback(input.TipoEscalera);
            decimal costeCamionGruaDia = config.MediosAuxiliares.CamionGruaDia > 0 ? config.MediosAuxiliares.CamionGruaDia : 0m;
            decimal costeAlquilerGatosDia = config.MediosAuxiliares.AlquilerGatosDia > 0 ? config.MediosAuxiliares.AlquilerGatosDia : 0m;
            decimal costeVehiculoAlquilerDia = config.MediosAuxiliares.VehiculoAlquilerDia > 0 ? config.MediosAuxiliares.VehiculoAlquilerDia : 0m;

            decimal alturaTanque = alturaPanel * input.NumeroAnillos;
            decimal perimetro = (decimal)Math.PI * input.DiametroMetros;
            decimal areaTecho = (decimal)Math.PI * input.DiametroMetros * input.DiametroMetros / 4m;

            var resultado = new PresupuestoInstalacionResultadoModel
            {
                AlturaPanelMetros = Round2(alturaPanel),
                LongitudPanelMetros = Round2(longitudPanel),
                AlturaTanqueMetros = Round2(alturaTanque),
                PerimetroTanqueMetros = Round2(perimetro),
                AreaTechoM2 = Round2(areaTecho)
            };

            var conteoEspesores = input.EspesoresAnillosMm
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count() * input.NumeroPlacasPorAnillo);

            decimal horasMontajePlacas = 0m;
            decimal horasCamionGrua = 0m;
            int numeroPanelesConGrua = 0;

            foreach (var item in conteoEspesores.OrderBy(x => x.Key))
            {
                decimal espesorMm = item.Key;
                int numeroPlacas = item.Value;

                decimal pesoPanelKg = alturaPanel * longitudPanel * (espesorMm / 1000m) * densidadAcero;

                decimal horasPanelSinGrua = horasBasePorPlaca + (horasPor100KgSinGrua * pesoPanelKg / 100m);
                decimal horasPanelConGrua = horasBasePorPlaca + (horasPor100KgConGrua * pesoPanelKg / 100m);

                bool requiereGrua = pesoPanelKg >= umbralPesoPanelGrua;
                decimal horasUnitarias = requiereGrua ? horasPanelConGrua : horasPanelSinGrua;
                decimal horasTotalesEspesor = horasUnitarias * numeroPlacas;

                horasMontajePlacas += horasTotalesEspesor;

                if (requiereGrua)
                {
                    horasCamionGrua += horasTotalesEspesor;
                    numeroPanelesConGrua += numeroPlacas;
                }
            }

            decimal horasCambiosGato = input.NumeroAnillos * input.NumeroPlacasPorAnillo * horasCambioGato;

            decimal horasEscaleras = input.TipoEscalera switch
            {
                TipoEscaleraPresupuesto.SinEscalera => 0m,
                TipoEscaleraPresupuesto.Vertical => input.NumeroEscaleras * alturaTanque * horasEscaleraVerticalMetro,
                TipoEscaleraPresupuesto.Helicoidal => input.NumeroEscaleras * alturaTanque * horasEscaleraHelicoidalMetro,
                _ => 0m
            };

            decimal horasConexiones =
                (input.ConexionesDn25a150 * horasConexion25a150) +
                (input.ConexionesDn150a300 * horasConexion150a300) +
                (input.ConexionesDn300a500 * horasConexion300a500) +
                (input.ConexionesMayor500 * horasConexionMayor500) +
                (input.NumeroBocasHombre * horasBocaHombre);

            decimal horasMontajeStarterRing =
                input.TieneStarterRing && horasStarterRing > 0m
                    ? input.NumeroPlacasPorAnillo * horasStarterRing
                    : 0m;

            int numeroLineasRigidizador = Math.Max(0, input.NumeroLineasRigidizador);
            decimal horasRigidizadores = numeroLineasRigidizador * input.NumeroPlacasPorAnillo * horasPorRigidizador;

            decimal horasAnclaje = perimetro * horasAnclajePorMetro;
            decimal horasSellado = perimetro * horasSelladoPorMetro;

            decimal horasTechoEstructura = techo?.HorasEstructuraM2 > 0 ? areaTecho * techo.HorasEstructuraM2 : 0m;
            decimal horasTechoPaneles = techo?.HorasPanelM2 > 0 ? areaTecho * techo.HorasPanelM2 : 0m;
            decimal horasTotalesTecho = horasTechoEstructura + horasTechoPaneles;

            decimal horasTotalesDeposito =
                horasMontajePlacas +
                horasCambiosGato +
                horasEscaleras +
                horasConexiones +
                horasMontajeStarterRing +
                horasRigidizadores +
                horasAnclaje +
                horasSellado;

            decimal horasDescanso = 0m;
            if (input.TamanoCuadrilla > 0)
            {
                horasDescanso = ((horasTotalesTecho + horasTotalesDeposito) / (5m * input.TamanoCuadrilla))
                               * (input.HorasTrabajoPorDia * 2m);
            }

            if (input.PorcentajeLluvia > 0m)
            {
                horasDescanso += (horasTotalesTecho + horasTotalesDeposito) * input.PorcentajeLluvia;
            }

            decimal horasDesplazamientoBase = Math.Max(
                (decimal)Math.Ceiling((double)((horasTotalesTecho + horasTotalesDeposito + horasDescanso) / (input.HorasTrabajoPorDia * 60m))) * 2m * input.HorasTrabajoPorDia,
                input.HorasTrabajoPorDia * 2m * input.TamanoCuadrilla);

            decimal horasEquipoTechoPre = (decimal)Math.Ceiling((double)(horasTotalesTecho / input.TamanoCuadrilla));
            decimal diasTechoPre = (decimal)Math.Ceiling((double)(horasEquipoTechoPre / input.HorasTrabajoPorDia));
            decimal horasEquipoDepositoPre = (decimal)Math.Ceiling((double)(horasTotalesDeposito / input.TamanoCuadrilla));
            decimal diasDepositoPre = (decimal)Math.Ceiling((double)(horasEquipoDepositoPre / input.HorasTrabajoPorDia));
            decimal horasEquipoDescansoPre = (decimal)Math.Ceiling((double)(horasDescanso / input.TamanoCuadrilla));
            decimal diasDescansoPre = (decimal)Math.Ceiling((double)(horasEquipoDescansoPre / input.HorasTrabajoPorDia));

            decimal horasDesplazamientoAlojamiento = 0m;
            if (input.DistanciaAlojamientoObraHoras > 0m)
            {
                decimal diasConDesplazamiento = diasTechoPre + diasDepositoPre + diasDescansoPre;
                horasDesplazamientoAlojamiento = diasConDesplazamiento * input.TamanoCuadrilla * input.DistanciaAlojamientoObraHoras * 2m;
            }

            decimal horasDesplazamiento = horasDesplazamientoBase + horasDesplazamientoAlojamiento;

            decimal horasEquipoTecho = horasEquipoTechoPre;
            decimal diasTecho = diasTechoPre;

            decimal horasEquipoDeposito = horasEquipoDepositoPre;
            decimal diasDeposito = diasDepositoPre;

            decimal horasEquipoDescanso = horasEquipoDescansoPre;
            decimal diasDescanso = diasDescansoPre;

            decimal horasEquipoDesplazamiento = (decimal)Math.Ceiling((double)(horasDesplazamiento / input.TamanoCuadrilla));
            decimal diasDesplazamiento = (decimal)Math.Ceiling((double)(horasEquipoDesplazamiento / input.HorasTrabajoPorDia));

            decimal diasTotalesExcel = diasTecho + diasDeposito;

            resultado.Horas = new HorasInstalacionDetalleModel
            {
                HorasMontajePlacas = Round2(horasMontajePlacas),
                HorasCambiosGato = Round2(horasCambiosGato),
                HorasEscaleras = Round2(horasEscaleras),
                HorasConexionesYBocaHombre = Round2(horasConexiones),
                HorasStarterRing = Round2(horasMontajeStarterRing),
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

            decimal divisorDias = (diasTecho + diasDeposito) <= 0m ? 1m : (diasTecho + diasDeposito);
            decimal diasRepartidosTecho = Math.Round((diasTotalesExcel / divisorDias) * diasTecho, 0, MidpointRounding.AwayFromZero);
            decimal diasRepartidosDeposito = Math.Round((diasTotalesExcel / divisorDias) * diasDeposito, 0, MidpointRounding.AwayFromZero);

            decimal costeMontajeTecho = diasRepartidosTecho * input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario;
            decimal costeMontajeDeposito = diasRepartidosDeposito * input.HorasTrabajoPorDia * input.TamanoCuadrilla * costeHoraOperario;

            decimal costeSiteManager = input.NumeroSiteManagers * diasTotalesExcel * input.HorasTrabajoPorDia * costeHoraIngeniero;
            decimal costeSeguridad = input.NumeroTecnicosSeguridad * diasTotalesExcel * input.HorasTrabajoPorDia * costeHoraSeguridad;

            decimal numeroVuelos = (horasDesplazamientoBase + ((input.NumeroSiteManagers + input.NumeroTecnicosSeguridad) * input.HorasTrabajoPorDia * 2m))
                                 / input.HorasTrabajoPorDia;

            decimal costeVuelos = numeroVuelos * costeVueloUnitario;

            decimal costeFabricacionEscalera = input.TipoEscalera switch
            {
                TipoEscaleraPresupuesto.SinEscalera => 0m,
                _ => input.NumeroEscaleras * alturaTanque * costeFabricacionEscaleraMetro
            };

            decimal diasCamionGrua = 0m;
            if (costeCamionGruaDia > 0m && numeroPanelesConGrua > 0)
            {
                decimal diasPorPaneles = panelesPorDiaCamionGrua > 0m
                    ? (decimal)Math.Ceiling(numeroPanelesConGrua / panelesPorDiaCamionGrua)
                    : 0m;

                decimal diasPorHoras = input.TamanoCuadrilla > 0 && input.HorasTrabajoPorDia > 0
                    ? (decimal)Math.Ceiling((double)(horasCamionGrua / (input.TamanoCuadrilla * input.HorasTrabajoPorDia)))
                    : 0m;

                diasCamionGrua = Math.Max(diasPorPaneles, diasPorHoras);
            }

            decimal diasAlquilerGatos = diasDeposito;
            decimal diasVehiculoAlquiler = input.DistanciaAlojamientoObraHoras > 0m
                ? (diasTecho + diasDeposito + diasDescanso + diasDesplazamiento)
                : 0m;

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
                    costeFabricacionEscaleraMetro, costeFabricacionEscalera),

                CrearPartida("INST-07", "Camión grúa", diasCamionGrua, "día",
                    costeCamionGruaDia, diasCamionGrua * costeCamionGruaDia),

                CrearPartida("INST-08", "Alquiler gatos", diasAlquilerGatos, "día",
                    costeAlquilerGatosDia, diasAlquilerGatos * costeAlquilerGatosDia),

                CrearPartida("INST-09", "Vehículo alquiler", diasVehiculoAlquiler, "día",
                    costeVehiculoAlquilerDia, diasVehiculoAlquiler * costeVehiculoAlquilerDia)
            };

            if (input.CosteTransporteManual > 0m)
            {
                partidas.Add(CrearPartida("INST-10", "Transporte", 1m, "ud", input.CosteTransporteManual, input.CosteTransporteManual));
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

        private PanelFabricantePresupuestoJsonModel? ObtenerPanelFabricante(PresupuestoConfigJsonModel config, FabricantePresupuesto fabricante)
        {
            string nombre = MapearFabricante(fabricante);

            return config.PanelesFabricante
                .FirstOrDefault(x => NormalizarClave(x.Fabricante) == NormalizarClave(nombre));
        }

        private static TechoPresupuestoJsonModel? ObtenerTecho(PresupuestoConfigJsonModel config, TipoTechoPresupuesto tipoTecho)
        {
            string nombre = tipoTecho switch
            {
                TipoTechoPresupuesto.SinTecho => "Sin techo",
                TipoTechoPresupuesto.Conico => "Conico",
                TipoTechoPresupuesto.Plano => "Plano",
                TipoTechoPresupuesto.DomoGeodesico => "Domo geodesico",
                _ => "Sin techo"
            };

            return config.Techo.FirstOrDefault(x => NormalizarClave(x.Tipo) == NormalizarClave(nombre));
        }

        private static EscaleraPresupuestoJsonModel? ObtenerEscalera(PresupuestoConfigJsonModel config, TipoEscaleraPresupuesto tipoEscalera)
        {
            string nombre = tipoEscalera switch
            {
                TipoEscaleraPresupuesto.SinEscalera => "Sin escalera",
                TipoEscaleraPresupuesto.Vertical => "Vertical",
                TipoEscaleraPresupuesto.Helicoidal => "Helicoidal",
                _ => "Sin escalera"
            };

            return config.Escaleras.FirstOrDefault(x => NormalizarClave(x.Tipo) == NormalizarClave(nombre));
        }

        private static decimal ObtenerCosteHoraOperario(PresupuestoConfigJsonModel config, UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? (config.CostesManoObra.OperarioNacional > 0 ? config.CostesManoObra.OperarioNacional : 32m)
                : (config.CostesManoObra.TrabajadorInternacional > 0 ? config.CostesManoObra.TrabajadorInternacional : 45m);
        }

        private static decimal ObtenerCosteHoraIngeniero(PresupuestoConfigJsonModel config, UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? (config.CostesManoObra.IngenieroNacional > 0 ? config.CostesManoObra.IngenieroNacional : 40m)
                : (config.CostesManoObra.IngenieroInternacional > 0 ? config.CostesManoObra.IngenieroInternacional : 50m);
        }

        private static decimal ObtenerCosteHoraSeguridad(PresupuestoConfigJsonModel config, UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion == UbicacionObraPresupuesto.Nacional
                ? (config.CostesManoObra.TecnicoSeguridadNacional > 0 ? config.CostesManoObra.TecnicoSeguridadNacional : 45m)
                : (config.CostesManoObra.TecnicoSeguridadInternacional > 0 ? config.CostesManoObra.TecnicoSeguridadInternacional : 55m);
        }

        private static decimal ObtenerCosteVuelo(PresupuestoConfigJsonModel config, UbicacionObraPresupuesto ubicacion)
        {
            return ubicacion switch
            {
                UbicacionObraPresupuesto.Nacional => config.Vuelos.Nacional,
                UbicacionObraPresupuesto.Europa => config.Vuelos.Europa > 0 ? config.Vuelos.Europa : 500m,
                UbicacionObraPresupuesto.Internacional => config.Vuelos.Internacional > 0 ? config.Vuelos.Internacional : 1000m,
                _ => 0m
            };
        }

        private static decimal ObtenerAlturaPanelFallback(FabricantePresupuesto fabricante)
        {
            return fabricante switch
            {
                FabricantePresupuesto.Balmoral => 1.2m,
                FabricantePresupuesto.Permastore => 1.4m,
                FabricantePresupuesto.DL2 => 1.4m,
                _ => 1.4m
            };
        }

        private static decimal ObtenerLongitudPanelFallback(FabricantePresupuesto fabricante)
        {
            return fabricante switch
            {
                FabricantePresupuesto.Balmoral => 2.45m,
                FabricantePresupuesto.Permastore => 2.68m,
                FabricantePresupuesto.DL2 => 2.68m,
                _ => 2.68m
            };
        }

        private static decimal ObtenerPrecioEscaleraFallback(TipoEscaleraPresupuesto tipoEscalera)
        {
            return tipoEscalera switch
            {
                TipoEscaleraPresupuesto.Vertical => 250m,
                TipoEscaleraPresupuesto.Helicoidal => 980m,
                _ => 0m
            };
        }

        private static string MapearFabricante(FabricantePresupuesto fabricante)
        {
            return fabricante switch
            {
                FabricantePresupuesto.Balmoral => "BALMORAL",
                FabricantePresupuesto.Permastore => "PERMASTORE",
                FabricantePresupuesto.DL2 => "DL2",
                _ => "BALMORAL"
            };
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

        private static decimal Round2(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
