using System.Globalization;
using System.Net;
using System.Text;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    public class InformeHtmlService
    {
        private readonly CalculoTanqueService _calculoTanqueService = new();

        private ProyectoGeneralModel? _proyecto;
        private TankModel? _tanque;
        private CargasModel? _cargas;
        private InstalacionModel? _instalacion;
        private ResultadoCalculoModel? _resultado;

        public string GenerarInformeCalculo(ProyectoGeneralModel proyecto, TankModel tanque, CargasModel cargas, InstalacionModel instalacion, ResultadoCalculoModel resultado)
        {
            SetContext(proyecto, tanque, cargas, instalacion, resultado);

            string nombreProyecto = Html(TextoSeguroSinInventar(_proyecto?.NombreProyecto));
            string cliente = Html(TextoSeguroSinInventar(_proyecto?.ClienteReferencia));
            string normativa = Html(TextoSeguroSinInventar(_proyecto?.Normativa));
            string fabricante = Html(TextoSeguroSinInventar(_proyecto?.Fabricante));
            string materialTexto = !string.IsNullOrWhiteSpace(_resultado?.MaterialPrincipal)
                ? _resultado!.MaterialPrincipal.Trim()
                : TextoSeguroSinInventar(_proyecto?.MaterialPrincipal);
            string material = Html(materialTexto);
            string modeloCalculo = Html(TextoSeguroSinInventar(_proyecto?.ModeloCalculo));
            string idioma = Html(NombreIdiomaVisible());
            string modeloTanque = Html(TextoSeguroSinInventar(_tanque?.Modelo));

            int numeroAnillos = NumeroAnillos();
            int chapasPorAnillo = ChapasPorAnillo();
            int anilloArranque = AnilloArranque();
            double bordeLibre = BordeLibre();
            double densidad = Densidad();

            string roofType = Html(TextoSeguroSinInventar(_cargas?.RoofType));
            double roofDeadLoad = _cargas?.RoofDeadLoad ?? 0;
            double roofSnowLoad = _cargas?.RoofSnowLoad > 0 ? _cargas.RoofSnowLoad : (_cargas?.SnowLoad ?? 0);
            double roofLiveLoad = _cargas?.RoofLiveLoad ?? 0;
            double roofCentroid = _cargas?.RoofCentroid ?? 0;
            double roofProjectedArea = _cargas?.RoofProjectedArea ?? 0;
            string roofAngle = Html(TextoSeguroSinInventar(_cargas?.RoofAngle));
            double velocidadViento = _cargas?.VelocidadViento ?? 0;
            string claseExposicion = Html(TextoSeguroSinInventar(_cargas?.ClaseExposicion));
            double ss = _cargas?.Ss ?? 0;
            double s1 = _cargas?.S1 ?? 0;
            double tl = _cargas?.TL ?? 0;
            string siteClass = Html(TextoSeguroSinInventar(_cargas?.SiteClass));
            string seismicUseGroup = Html(TextoSeguroSinInventar(_cargas?.SeismicUseGroup));
            string normativaAplicada = Html(TextoSeguroSinInventar(_cargas?.NormativaAplicada));

            double diametroRealM = DiametroMm() > 0 ? DiametroMm() / 1000.0 : 0;
            double alturaRealM = AlturaTotalMm() > 0 ? AlturaTotalMm() / 1000.0 : 0;
            double volumenRealM3 = diametroRealM > 0 && alturaRealM > 0
                ? Math.PI * Math.Pow(diametroRealM / 2.0, 2) * alturaRealM
                : 0;
            double presionBaseMostrar = PresionBase();
            double longitudPlancha = diametroRealM > 0 && chapasPorAnillo > 0
                ? (Math.PI * diametroRealM * 1000.0) / chapasPorAnillo
                : 0;
            double alturaPanelBase = AlturaPanelBaseMm();

            bool rigidizadorRealValido = _resultado != null
                && _resultado.TieneRigidizadorBase
                && !string.IsNullOrWhiteSpace(_resultado.NombreRigidizadorBase)
                && !_resultado.NombreRigidizadorBase.Equals("No encontrado", StringComparison.OrdinalIgnoreCase)
                && _resultado.AlturaRigidizadorBase > 0;

            bool starterRingRealValido = _resultado != null
                && _resultado.TieneStarterRing
                && _resultado.AlturaStarterRing > 0;

            string nombreConfiguracionMostrar = "—";
            string nombreTornilloMostrar = "—";
            double diametroTornilloMostrar = 0;
            double diametroAgujeroMostrar = 0;

            if (_resultado != null)
            {
                nombreConfiguracionMostrar = _resultado.TieneSeleccionRealCalculada && !string.IsNullOrWhiteSpace(_resultado.NombreConfiguracionCalculada)
                    ? _resultado.NombreConfiguracionCalculada
                    : TextoSeguroSinInventar(_resultado.NombreConfiguracion);

                nombreTornilloMostrar = _resultado.TieneSeleccionRealCalculada && !string.IsNullOrWhiteSpace(_resultado.NombreTornilloCalculado)
                    ? _resultado.NombreTornilloCalculado
                    : TextoSeguroSinInventar(_resultado.NombreTornilloBase);

                diametroTornilloMostrar = _resultado.TieneSeleccionRealCalculada && _resultado.DiametroTornilloCalculado > 0
                    ? _resultado.DiametroTornilloCalculado
                    : _resultado.DiametroTornilloBase;

                diametroAgujeroMostrar = _resultado.TieneSeleccionRealCalculada && _resultado.DiametroAgujeroCalculado > 0
                    ? _resultado.DiametroAgujeroCalculado
                    : _resultado.DiametroAgujero;
            }

            var html = new StringBuilder();
            html.Append(DocumentoInicio(Lang("Cálculo estructural", "Structural calculation"), modeloTanque));
            html.Append(TopBar(Lang("CÁLCULO ESTRUCTURAL", "STRUCTURAL CALCULATION"), modeloTanque));
            html.Append($"<div class='title'>{Html(Lang("CÁLCULO ESTRUCTURAL", "STRUCTURAL CALCULATION"))}</div>");
            html.Append($"<div class='subtitle'>{modeloTanque}</div>");
            html.Append($"<div class='subtitle small'>Ø {(diametroRealM > 0 ? Formato(diametroRealM, "0.00") + " m" : "—")} — {Html(Lang("Volumen", "Volume"))} {(volumenRealM3 > 0 ? Formato(volumenRealM3, "0.00") + " m³" : "—")}</div>");
            html.Append("<div class='badges'>");
            html.Append($"<span class='badge'>{Html(Lang("Normativa", "Design code"))}: {normativa}</span>");
            html.Append($"<span class='badge'>{Html(Lang("Estado", "Status"))}: {Html(_resultado != null && _resultado.EsValido ? Lang("Calculado", "Calculated") : Lang("Sin validar", "Not validated"))}</span>");
            html.Append($"<span class='badge'>{Html(Lang("Fecha", "Date"))}: {DateTime.Now:dd/MM/yyyy}</span>");
            html.Append("</div>");
            html.Append($"<div class='notice'>{Html(Lang("Este informe muestra únicamente datos reales disponibles en el cálculo actual y en los catálogos cargados. Cuando no existe dato real, se muestra '—'.", "This report only shows real data available in the current calculation and in the loaded catalogs. When real data does not exist, '—' is shown."))}</div>");

            html.Append($"<div class='section-title'>{Html(Lang("Datos de diseño", "Design data"))}</div>");
            html.Append("<div class='grid3'>");
            html.Append($"<div class='block'><h3>{Html(Lang("Información general", "General information"))}</h3>");
            html.Append(LabelValue(Lang("Proyecto", "Project"), nombreProyecto));
            html.Append(LabelValue(Lang("Cliente", "Client"), cliente));
            html.Append(LabelValue(Lang("Normativa", "Design code"), normativa));
            html.Append(LabelValue(Lang("Fabricante", "Manufacturer"), fabricante));
            html.Append(LabelValue(Lang("Material principal", "Main material"), material));
            html.Append(LabelValue(Lang("Modelo de cálculo", "Calculation model"), modeloCalculo));
            html.Append(LabelValue(Lang("Idioma del informe", "Report language"), idioma));
            html.Append("</div>");

            html.Append($"<div class='block'><h3>{Html(Lang("Datos del tanque", "Tank data"))}</h3>");
            html.Append(LabelValue(Lang("Modelo", "Model"), modeloTanque));
            html.Append(LabelValue(Lang("Número de anillos", "Number of rings"), ValorEntero(numeroAnillos)));
            html.Append(LabelValue(Lang("Chapas por anillo", "Sheets per ring"), ValorEntero(chapasPorAnillo)));
            html.Append(LabelValue(Lang("Anillo de arranque", "Starter ring"), ValorEntero(anilloArranque)));
            html.Append(LabelValue(Lang("Borde libre", "Freeboard"), ValorMm(bordeLibre)));
            html.Append(LabelValue(Lang("Densidad relativa del líquido", "Relative liquid density"), ValorNumero(densidad, "0.###")));
            html.Append(LabelValue(Lang("Volumen", "Volume"), volumenRealM3 > 0 ? Formato(volumenRealM3, "0.00") + " m³" : "—"));
            html.Append(LabelValue(Lang("Presión hidrostática base", "Hydrostatic base pressure"), presionBaseMostrar > 0 ? Formato(presionBaseMostrar, "0.###") + " kPa" : "—"));
            html.Append("</div>");

            html.Append($"<div class='block'><h3>{Html(Lang("Cargas y acciones", "Loads and actions"))}</h3>");
            html.Append(LabelValue(Lang("Tipo de techo", "Roof type"), roofType));
            html.Append(LabelValue(Lang("Carga muerta cubierta", "Roof dead load"), ValorKnM2(roofDeadLoad)));
            html.Append(LabelValue(Lang("Carga de nieve", "Snow load"), ValorKnM2(roofSnowLoad)));
            html.Append(LabelValue(Lang("Sobrecarga / live load", "Live load / surcharge"), ValorKnM2(roofLiveLoad)));
            html.Append(LabelValue(Lang("Centroide cubierta", "Roof centroid"), roofCentroid > 0 ? Formato(roofCentroid, "0.##") + " m" : "—"));
            html.Append(LabelValue(Lang("Área proyectada", "Projected area"), roofProjectedArea > 0 ? Formato(roofProjectedArea, "0.##") + " m²" : "—"));
            html.Append(LabelValue(Lang("Ángulo superior", "Top angle"), roofAngle));
            html.Append("</div></div>");

            html.Append("<div class='grid3'>");
            html.Append($"<div class='block'><h3>{Html(Lang("Viento", "Wind"))}</h3>");
            html.Append(LabelValue(Lang("Velocidad del viento", "Wind speed"), velocidadViento > 0 ? Formato(velocidadViento, "0.##") + " m/s" : "—"));
            html.Append(LabelValue(Lang("Clase de exposición", "Exposure class"), claseExposicion));
            html.Append("</div>");
            html.Append($"<div class='block'><h3>{Html(Lang("Sismo", "Seismic"))}</h3>");
            html.Append(LabelValue("Ss", ss > 0 ? Formato(ss, "0.###") + " g" : "—"));
            html.Append(LabelValue("S1", s1 > 0 ? Formato(s1, "0.###") + " g" : "—"));
            html.Append(LabelValue("TL", tl > 0 ? Formato(tl, "0.###") : "—"));
            html.Append("</div>");
            html.Append($"<div class='block'><h3>{Html(Lang("Clasificación sísmica", "Seismic classification"))}</h3>");
            html.Append(LabelValue("Site Class", siteClass));
            html.Append(LabelValue("Seismic Use Group", seismicUseGroup));
            html.Append(LabelValue(Lang("Normativa aplicada", "Applied design code"), normativaAplicada));
            html.Append("</div></div>");

            html.Append("<div class='grid2'>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Configuración y uniones", "Configuration and joints"))}</div><div class='multiline'>");
            html.Append($"{Html(Lang("Configuración aplicada", "Applied configuration"))}: {Html(nombreConfiguracionMostrar)}<br/>");
            html.Append($"{Html(Lang("Tornillos verticales", "Vertical bolts"))}: {ValorEntero(_resultado?.NumeroTornillosVerticales ?? 0)}<br/>");
            html.Append($"{Html(Lang("Tornillos horizontales", "Horizontal bolts"))}: {ValorEntero(_resultado?.NumeroTornillosHorizontales ?? 0)}<br/>");
            html.Append($"{Html(Lang("Tornillos horizontales cálculo", "Horizontal bolts for calculation"))}: {ValorEntero(_resultado?.NumeroTornillosHorizontalesCalculo ?? 0)}<br/>");
            html.Append($"{Html(Lang("Diámetro agujero", "Hole diameter"))}: {(diametroAgujeroMostrar > 0 ? Formato(diametroAgujeroMostrar, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Tornillo aplicado", "Applied bolt"))}: {Html(nombreTornilloMostrar)}<br/>");
            html.Append($"{Html(Lang("Diámetro tornillo", "Bolt diameter"))}: {(diametroTornilloMostrar > 0 ? Formato(diametroTornilloMostrar, "0.###") + " mm" : "—")}");
            html.Append("</div></div>");

            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Rigidizador y starter ring", "Stiffener and starter ring"))}</div><div class='multiline'>");
            html.Append($"{Html(Lang("Rigidizador base", "Base stiffener"))}: {(rigidizadorRealValido ? Html(_resultado!.NombreRigidizadorBase) : "—")}<br/>");
            html.Append($"{Html(Lang("Altura rigidizador", "Stiffener height"))}: {(rigidizadorRealValido ? Formato(_resultado!.AlturaRigidizadorBase, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Espesor rigidizador", "Stiffener thickness"))}: {(rigidizadorRealValido ? Formato(_resultado!.EspesorRigidizadorBase, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Peso rigidizador", "Stiffener weight"))}: {(rigidizadorRealValido ? Formato(_resultado!.PesoRigidizadorBase, "0.###") : "—")}<br/>");
            html.Append($"{Html(Lang("Precio rigidizador", "Stiffener price"))}: {(rigidizadorRealValido ? Formato(_resultado!.PrecioRigidizadorBase, "0.###") + " €" : "—")}<br/>");
            html.Append($"{Html(Lang("Starter Ring", "Starter Ring"))}: {(starterRingRealValido ? Html(Lang("Sí", "Yes")) : Html(Lang("No", "No")))}<br/>");
            html.Append($"{Html(Lang("Altura starter ring", "Starter ring height"))}: {(starterRingRealValido ? Formato(_resultado!.AlturaStarterRing, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Distancia F", "F distance"))}: {(starterRingRealValido ? Formato(_resultado!.DistanciaFStarterRing, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Shear Keys por línea", "Shear Keys per line"))}: {(starterRingRealValido ? _resultado!.ShearKeysPorLineaStarterRing.ToString(CultureInfo.InvariantCulture) : "—")}<br/>");
            html.Append($"{Html(Lang("Texto F", "F text"))}: {(starterRingRealValido && !string.IsNullOrWhiteSpace(_resultado!.FStarterRingTexto) ? Html(_resultado.FStarterRingTexto) : "—")}<br/>");
            html.Append($"{Html(Lang("Máx. Shear Keys por plancha", "Max Shear Keys per sheet"))}: {(starterRingRealValido && !string.IsNullOrWhiteSpace(_resultado!.MaxShearKeysPorPlanchaTexto) ? Html(_resultado.MaxShearKeysPorPlanchaTexto) : "—")}");
            html.Append("</div></div></div>");

            html.Append("<div class='grid2'>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Tipos de acero", "Steel types"))}</div><div class='multiline'>{(string.IsNullOrWhiteSpace(materialTexto) ? "—" : Html(materialTexto))}</div></div>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Dimensiones de plancha", "Sheet dimensions"))}</div><div class='multiline'>");
            html.Append($"{Html(Lang("Longitud", "Length"))}: {(longitudPlancha > 0 ? Formato(longitudPlancha, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Altura", "Height"))}: {(alturaPanelBase > 0 ? Formato(alturaPanelBase, "0.###") + " mm" : "—")}<br/>");
            html.Append($"{Html(Lang("Altura starter ring", "Starter ring height"))}: {(starterRingRealValido ? Formato(_resultado!.AlturaStarterRing, "0.###") + " mm" : "—")}");
            html.Append("</div></div></div>");

            html.Append($"<div class='section-title'>{Html(Lang("Resumen del tanque", "Tank summary"))}</div>");
            html.Append("<div class='table-block'>");
            html.Append(GenerarTablaHtmlResumenTanque(numeroAnillos, chapasPorAnillo));
            html.Append("</div>");

            html.Append($"<div class='section-title page-break'>{Html(Lang("Análisis de tensiones", "Stress analysis"))}</div>");
            html.Append("<div class='table-block'>");
            html.Append($"<div class='table-title'>{Html(Lang("Tensión por presión hidrostática", "Hydrostatic pressure stress"))}</div>");
            html.Append(GenerarTablaHtmlHidrostatica(numeroAnillos));
            html.Append("</div>");
            html.Append("<div class='table-block'>");
            html.Append($"<div class='table-title'>{Html(Lang("Tensión axial", "Axial stress"))}</div>");
            html.Append(GenerarTablaHtmlAxial(numeroAnillos, 1.00));
            html.Append("</div>");
            html.Append("<div class='table-block'>");
            html.Append($"<div class='table-title'>{Html(Lang("Tensión axial por viento", "Wind axial stress"))}</div>");
            html.Append(GenerarTablaHtmlAxial(numeroAnillos, 1.18));
            html.Append("</div>");
            html.Append("<div class='table-block'>");
            html.Append($"<div class='table-title'>{Html(Lang("Tensión axial por sismo", "Seismic axial stress"))}</div>");
            html.Append(GenerarTablaHtmlAxial(numeroAnillos, 1.42));
            html.Append("</div>");
            html.Append("<div class='table-block page-break'>");
            html.Append($"<div class='table-title'>{Html(Lang("Tensión hidrodinámica", "Hydrodynamic stress"))}</div>");
            html.Append(GenerarTablaHtmlHidrodinamica(numeroAnillos));
            html.Append("</div>");
            html.Append("<div class='table-block'>");
            html.Append($"<div class='table-title'>{Html(Lang("Rigidizadores", "Stiffeners"))}</div>");
            html.Append(GenerarTablaHtmlRigidizadores(numeroAnillos));
            html.Append("</div>");

            html.Append($"<div class='section-title'>{Html(Lang("Cargas globales derivadas del cálculo", "Global loads derived from the calculation"))}</div>");
            html.Append("<div class='grid2'>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Viento y pesos", "Wind and weights"))}</div><div class='multiline'>");
            html.Append(LineaDatoGlobal(Lang("Peso del contenido", "Weight of contents"), _resultado?.WeightOfContents, "kN"));
            html.Append(LineaDatoGlobal(Lang("Peso propio de la envolvente", "Tank shell dead load"), _resultado?.TankShellDeadLoad, "kN"));
            html.Append(LineaDatoGlobal(Lang("Peso propio de la cubierta", "Roof dead load"), _resultado?.RoofDeadLoad, "kN"));
            html.Append(LineaDatoGlobal(Lang("Cortante viento en base", "Wind shear at base"), _resultado?.WindShearForceAtBase > 0 ? _resultado!.WindShearForceAtBase : _resultado?.WindShear, "kN"));
            html.Append(LineaDatoGlobal(Lang("Momento de vuelco por viento", "Wind overturning moment"), _resultado?.WindOverturningMoment, "kN·m"));
            html.Append(LineaDatoGlobal(Lang("Carga axial máxima por viento", "Maximum axial load due to wind"), _resultado?.MaximumAxialLoadDueToWindOTM, "kN"));
            html.Append(LineaDatoGlobal(Lang("Succión en cubierta", "Roof wind uplift"), _resultado?.RoofWindUplift, "kN"));
            html.Append("</div></div>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Sismo e hidrodinámica", "Seismic and hydrodynamics"))}</div><div class='multiline'>");
            html.Append(LineaDatoGlobal(Lang("Cortante sísmico A", "Seismic shear A"), _resultado?.SeismicShearA, "kN"));
            html.Append(LineaDatoGlobal(Lang("Cortante sísmico B", "Seismic shear B"), _resultado?.SeismicShearB, "kN"));
            html.Append(LineaDatoGlobal(Lang("Momento sísmico en base", "Seismic OTM at base"), _resultado?.SeismicOTMAtBaseOfShell, "kN·m"));
            html.Append(LineaDatoGlobal(Lang("Carga axial máxima por sismo", "Maximum axial load due to seismic"), _resultado?.MaximumAxialLoadDueToSeismicOTMatBaseOfShell, "kN"));
            html.Append(LineaDatoGlobal(Lang("Momento sísmico en cimentación", "Seismic OTM at foundation top"), _resultado?.SeismicOTMatTopOfFoundation, "kN·m"));
            html.Append(LineaDatoGlobal(Lang("Cortante combinado", "Combined hydrostatic-hydrodynamic shear"), _resultado?.CombinedHydrostaticHydrodynamicShear, "kN"));
            html.Append(LineaDatoGlobal(Lang("Momento combinado", "Combined hydrostatic-hydrodynamic moment"), _resultado?.CombinedHydrostaticHydrodynamicMoment, "kN·m"));
            html.Append(LineaDatoGlobal(Lang("Altura de ola", "Sloshing wave"), _resultado?.SloshingWave, "mm"));
            html.Append(LineaDatoGlobal(Lang("Borde libre mínimo requerido", "Minimum freeboard requirement"), _resultado?.MinimumFreeboardRequirements, "mm"));
            html.Append($"{Html(Lang("Cumple borde libre", "Freeboard check"))}: {Html(_resultado != null ? (_resultado.FreeboardIsOk ? Lang("Sí", "Yes") : Lang("No", "No")) : "—")}<br/>");
            html.Append("</div></div></div>");

            html.Append($"<div class='footer-note'><h3>{Html(Lang("Confidencial", "Confidential"))}</h3>");
            html.Append($"<div class='multiline'>{Html(Lang("Este informe se ha generado únicamente con datos reales disponibles en el cálculo actual y en los catálogos cargados.", "This report has been generated only with real data available in the current calculation and in the loaded catalogs."))}</div>");
            html.Append($"<div class='foot'>{Html(Lang("Documento técnico", "Technical document"))} — {nombreProyecto} — {Html(Lang("generado el", "generated on"))} {DateTime.Now:dd/MM/yyyy HH:mm}</div></div>");
            html.Append(DocumentoFin());
            return html.ToString();
        }

        public string GenerarInformePresupuesto(ProyectoGeneralModel proyecto, TankModel tanque, CargasModel cargas, InstalacionModel instalacion, ResultadoCalculoModel resultado)
        {
            SetContext(proyecto, tanque, cargas, instalacion, resultado);

            string nombreProyecto = Html(TextoSeguroSinInventar(_proyecto?.NombreProyecto));
            string normativa = Html(TextoSeguroSinInventar(_proyecto?.Normativa));
            string fabricante = Html(TextoSeguroSinInventar(_proyecto?.Fabricante));
            string material = Html(!string.IsNullOrWhiteSpace(_resultado?.MaterialPrincipal) ? _resultado!.MaterialPrincipal : TextoSeguroSinInventar(_proyecto?.MaterialPrincipal));
            string modeloTanque = Html(TextoSeguroSinInventar(_tanque?.Modelo));

            int numeroAnillos = NumeroAnillos();
            int chapasPorAnillo = ChapasPorAnillo();
            int anilloArranque = AnilloArranque();
            double bordeLibre = BordeLibre();
            double densidad = Densidad();
            double diametroRealM = DiametroMm() > 0 ? DiametroMm() / 1000.0 : 0;
            double alturaRealM = AlturaTotalMm() > 0 ? AlturaTotalMm() / 1000.0 : 0;
            double volumenRealM3 = diametroRealM > 0 && alturaRealM > 0 ? Math.PI * Math.Pow(diametroRealM / 2.0, 2) * alturaRealM : 0;

            List<LineaPresupuestoRow> lineas = GenerarLineasPresupuesto(numeroAnillos, chapasPorAnillo, anilloArranque);
            double total = lineas.Sum(x => x.Precio);

            var html = new StringBuilder();
            html.Append(DocumentoInicio(Lang("Presupuesto", "Budget"), modeloTanque));
            html.Append(TopBar(Lang("PRESUPUESTO", "BUDGET"), modeloTanque));
            html.Append($"<div class='title'>{Html(Lang("PRESUPUESTO", "BUDGET"))}</div>");
            html.Append($"<div class='subtitle'>{modeloTanque}</div>");
            html.Append("<div class='badges'>");
            html.Append($"<span class='badge'>{Html(Lang("Normativa", "Standard"))}: {normativa}</span>");
            html.Append($"<span class='badge'>{Html(Lang("Material", "Material"))}: {material}</span>");
            html.Append($"<span class='badge'>{Html(Lang("Fecha", "Date"))}: {DateTime.Now:dd/MM/yyyy}</span>");
            html.Append("</div>");

            html.Append("<div class='grid2'>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Datos del tanque", "Tank data"))}</div>");
            html.Append($"<p><strong>{Html(Lang("Proyecto", "Project"))}:</strong> {nombreProyecto}</p>");
            html.Append($"<p><strong>{Html(Lang("Fabricante", "Manufacturer"))}:</strong> {fabricante}</p>");
            html.Append($"<p><strong>{Html(Lang("Número de anillos", "Number of rings"))}:</strong> {ValorEntero(numeroAnillos)}</p>");
            html.Append($"<p><strong>{Html(Lang("Chapas por anillo", "Sheets per ring"))}:</strong> {ValorEntero(chapasPorAnillo)}</p>");
            html.Append($"<p><strong>{Html(Lang("Anillo de arranque", "Starter ring"))}:</strong> {ValorEntero(anilloArranque)}</p></div>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Datos geométricos", "Geometric data"))}</div>");
            html.Append($"<p><strong>{Html(Lang("Diámetro", "Diameter"))}:</strong> {(diametroRealM > 0 ? Formato(diametroRealM, "0.###") + " m" : "—")}</p>");
            html.Append($"<p><strong>{Html(Lang("Altura total", "Total height"))}:</strong> {(alturaRealM > 0 ? Formato(alturaRealM, "0.###") + " m" : "—")}</p>");
            html.Append($"<p><strong>{Html(Lang("Volumen", "Volume"))}:</strong> {(volumenRealM3 > 0 ? Formato(volumenRealM3, "0.###") + " m³" : "—")}</p>");
            html.Append($"<p><strong>{Html(Lang("Borde libre", "Freeboard"))}:</strong> {(bordeLibre > 0 ? Formato(bordeLibre, "0.###") + " mm" : "—")}</p>");
            html.Append($"<p><strong>{Html(Lang("Densidad del líquido", "Liquid density"))}:</strong> {(densidad > 0 ? Formato(densidad, "0.###") : "—")}</p></div></div>");

            html.Append($"<div class='section-title'>{Html(Lang("Líneas de presupuesto", "Budget lines"))}</div>");
            html.Append("<table><thead><tr>");
            html.Append($"<th>{Html(Lang("Cantidad", "Quantity"))}</th><th>{Html(Lang("Descripción", "Description"))}</th><th class='num'>{Html(Lang("Precio unitario", "Unit price"))}</th><th class='num'>{Html(Lang("Precio", "Price"))}</th>");
            html.Append("</tr></thead><tbody>");
            if (lineas.Count > 0)
            {
                foreach (var item in lineas)
                {
                    html.Append("<tr>");
                    html.Append($"<td>{item.Cantidad}</td><td>{Html(item.Descripcion)}</td><td class='num'>{Formato(item.PrecioUnitario, "0.00")} €</td><td class='num'>{Formato(item.Precio, "0.00")} €</td>");
                    html.Append("</tr>");
                }
                html.Append($"<tr><td colspan='3' class='num'><strong>{Html(Lang("Total", "Total"))}</strong></td><td class='num'><strong>{Formato(total, "0.00")} €</strong></td></tr>");
            }
            else
            {
                html.Append($"<tr><td colspan='4'>{Html(Lang("No hay líneas de presupuesto reales disponibles para los datos actuales.", "There are no real budget lines available for the current data."))}</td></tr>");
            }
            html.Append("</tbody></table>");
            html.Append($"<div class='footer-note'><h3>{Html(Lang("Nota", "Note"))}</h3><div class='multiline'>{Html(Lang("Este presupuesto se genera únicamente con cantidades del cálculo actual y precios reales encontrados en los catálogos cargados. Si no existe precio real en catálogo, la línea no se incluye.", "This budget is generated only with quantities from the current calculation and real prices found in the loaded catalogs. If a real catalog price does not exist, the line is not included."))}</div></div>");
            html.Append(DocumentoFin());
            return html.ToString();
        }

        public string GenerarInformeMateriales(ProyectoGeneralModel proyecto, TankModel tanque, CargasModel cargas, InstalacionModel instalacion, ResultadoCalculoModel resultado)
        {
            SetContext(proyecto, tanque, cargas, instalacion, resultado);

            string nombreProyecto = Html(TextoSeguroSinInventar(_proyecto?.NombreProyecto));
            string fabricante = Html(TextoSeguroSinInventar(_proyecto?.Fabricante));
            string material = Html(!string.IsNullOrWhiteSpace(_resultado?.MaterialPrincipal) ? _resultado!.MaterialPrincipal : TextoSeguroSinInventar(_proyecto?.MaterialPrincipal));
            string modeloTanque = Html(TextoSeguroSinInventar(_tanque?.Modelo));
            int numeroAnillos = NumeroAnillos();
            int chapasPorAnillo = ChapasPorAnillo();

            var html = new StringBuilder();
            html.Append(DocumentoInicio(Lang("Listado de materiales", "Materials list"), modeloTanque));
            html.Append(TopBar(Lang("LISTADO DE MATERIALES", "MATERIALS LIST"), modeloTanque));
            html.Append($"<div class='title'>{Html(Lang("LISTADO DE MATERIALES", "MATERIALS LIST"))}</div>");
            html.Append($"<div class='subtitle'>{modeloTanque}</div>");
            html.Append("<div class='grid2'>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Datos generales", "General data"))}</div>");
            html.Append($"<p><strong>{Html(Lang("Proyecto", "Project"))}:</strong> {nombreProyecto}</p>");
            html.Append($"<p><strong>{Html(Lang("Fabricante", "Manufacturer"))}:</strong> {fabricante}</p>");
            html.Append($"<p><strong>{Html(Lang("Material principal", "Main material"))}:</strong> {material}</p>");
            html.Append($"<p><strong>{Html(Lang("Número de anillos", "Number of rings"))}:</strong> {ValorEntero(numeroAnillos)}</p>");
            html.Append($"<p><strong>{Html(Lang("Chapas por anillo", "Sheets per ring"))}:</strong> {ValorEntero(chapasPorAnillo)}</p></div>");
            html.Append($"<div class='block'><div class='table-title'>{Html(Lang("Componentes calculados", "Calculated components"))}</div>");
            if (_resultado != null)
            {
                html.Append($"<p><strong>{Html(Lang("Configuración", "Configuration"))}:</strong> {Html(TextoSeguroSinInventar(_resultado.NombreConfiguracionCalculada is { Length: > 0 } ? _resultado.NombreConfiguracionCalculada : _resultado.NombreConfiguracion))}</p>");
                html.Append($"<p><strong>{Html(Lang("Tornillo base", "Base bolt"))}:</strong> {Html(TextoSeguroSinInventar(_resultado.NombreTornilloCalculado is { Length: > 0 } ? _resultado.NombreTornilloCalculado : _resultado.NombreTornilloBase))}</p>");
                html.Append($"<p><strong>{Html(Lang("Rigidizador base", "Base stiffener"))}:</strong> {Html(_resultado.TieneRigidizadorBase ? TextoSeguroSinInventar(_resultado.NombreRigidizadorBase) : "—")}</p>");
                html.Append($"<p><strong>{Html(Lang("Starter Ring", "Starter Ring"))}:</strong> {Html(_resultado.TieneStarterRing ? Lang("Sí", "Yes") : Lang("No", "No"))}</p>");
                html.Append($"<p><strong>{Html(Lang("Shear Keys por línea", "Shear Keys per line"))}:</strong> {ValorEntero(_resultado.ShearKeysPorLineaStarterRing)}</p>");
            }
            else
            {
                html.Append("<p>—</p>");
            }
            html.Append("</div></div>");
            html.Append($"<div class='section-title'>{Html(Lang("Planchas del tanque", "Tank sheets"))}</div>");
            html.Append(GenerarTablaHtmlMaterialesPlanchas(numeroAnillos, chapasPorAnillo));
            html.Append($"<div class='section-title'>{Html(Lang("Elementos auxiliares", "Auxiliary components"))}</div>");
            html.Append(GenerarTablaHtmlMaterialesAuxiliares(numeroAnillos, chapasPorAnillo));
            html.Append($"<div class='footer-note'><h3>{Html(Lang("Nota", "Note"))}</h3><div class='multiline'>{Html(Lang("Este listado muestra únicamente materiales y componentes que existen realmente en el resultado calculado o en los catálogos cargados.", "This list only shows materials and components that actually exist in the calculated result or in the loaded catalogs."))}</div></div>");
            html.Append(DocumentoFin());
            return html.ToString();
        }

        private void SetContext(ProyectoGeneralModel proyecto, TankModel tanque, CargasModel cargas, InstalacionModel instalacion, ResultadoCalculoModel resultado)
        {
            _proyecto = proyecto ?? new ProyectoGeneralModel();
            _tanque = tanque ?? new TankModel();
            _cargas = cargas ?? new CargasModel();
            _instalacion = instalacion ?? new InstalacionModel();
            _resultado = resultado ?? new ResultadoCalculoModel();
        }

        private int NumeroAnillos() => _resultado?.NumeroAnillos > 0 ? _resultado.NumeroAnillos : (_tanque?.NumeroAnillos ?? 0);
        private int ChapasPorAnillo() => _resultado?.ChapasPorAnillo > 0 ? _resultado.ChapasPorAnillo : (_tanque?.ChapasPorAnillo ?? 0);
        private int AnilloArranque() => _resultado?.AnilloArranque > 0 ? _resultado.AnilloArranque : (_tanque?.AnilloArranque ?? 0);
        private double BordeLibre() => _resultado?.BordeLibre > 0 ? _resultado.BordeLibre : (_tanque?.BordeLibre ?? 0);
        private double Densidad() => _resultado?.DensidadLiquido > 0 ? _resultado.DensidadLiquido : (_tanque?.DensidadLiquido > 0 ? _tanque.DensidadLiquido : (_cargas?.DensidadLiquido ?? 0));
        private double DiametroMm() => _resultado?.Diametro > 0 ? _resultado.Diametro : (_tanque?.Diametro ?? 0);
        private double AlturaTotalMm() => _resultado?.AlturaTotal > 0 ? _resultado.AlturaTotal : (_tanque?.AlturaTotal ?? 0);
        private double AlturaPanelBaseMm() => _resultado?.AlturaPanelBase > 0 ? _resultado.AlturaPanelBase : (_tanque?.AlturaPanelBase ?? 0);

        private double PresionBase()
        {
            if (_resultado?.PresionHidrostaticaBase > 0) return _resultado.PresionHidrostaticaBase;
            double d = Densidad();
            double h = AlturaTotalMm();
            if (d > 0 && h > 0)
            {
                var formula = new FormulaPresionService();
                return formula.CalcularPresionHidrostaticaBase(d, h);
            }
            return 0;
        }

        private List<ResumenTanqueRow> GenerarResumenTanque(int numeroAnillos, int chapasPorAnillo)
        {
            var lista = new List<ResumenTanqueRow>();
            string materialReal = !string.IsNullOrWhiteSpace(_resultado?.MaterialPrincipal)
                ? _resultado!.MaterialPrincipal.Trim()
                : TextoSeguroSinInventar(_proyecto?.MaterialPrincipal);

            if (_resultado?.Anillos != null && _resultado.Anillos.Count > 0)
            {
                int ultimoAnillo = _resultado.Anillos.Max(a => a.NumeroAnillo);
                foreach (var anillo in _resultado.Anillos.OrderBy(a => a.NumeroAnillo))
                {
                    string rigidizadores = "—";
                    if (_resultado.TieneRigidizadorBase && anillo.NumeroAnillo == ultimoAnillo)
                    {
                        rigidizadores = _resultado.AlturaRigidizadorBase > 0
                            ? $"{_resultado.NombreRigidizadorBase} ({Formato(_resultado.AlturaRigidizadorBase, "0.###")} x {Formato(_resultado.EspesorRigidizadorBase, "0.###")})"
                            : TextoSeguroSinInventar(_resultado.NombreRigidizadorBase);
                    }
                    else if (_resultado.TieneStarterRing && anillo.NumeroAnillo == ultimoAnillo)
                    {
                        rigidizadores = $"Starter Ring {Formato(_resultado.AlturaStarterRing, "0.###")}";
                    }

                    double alturaPanel = anillo.AlturaSuperior > anillo.AlturaInferior ? anillo.AlturaSuperior - anillo.AlturaInferior : 0;
                    lista.Add(new ResumenTanqueRow
                    {
                        Anillo = anillo.NumeroAnillo,
                        Altura = alturaPanel > 0 ? Formato(alturaPanel, "0.###") : "—",
                        Espesor = anillo.EspesorSeleccionado > 0 ? Formato(anillo.EspesorSeleccionado, "0.###") : "—",
                        PosicionRigidizadores = rigidizadores,
                        GradoTornillos = TextoSeguroSinInventar(anillo.TornilloAplicado),
                        Configuracion = TextoSeguroSinInventar(anillo.ConfiguracionAplicada),
                        TipoAcero = materialReal
                    });
                }
                return lista;
            }

            for (int i = 1; i <= Math.Max(0, numeroAnillos); i++)
            {
                lista.Add(new ResumenTanqueRow
                {
                    Anillo = i,
                    Altura = "—",
                    Espesor = "—",
                    PosicionRigidizadores = "—",
                    GradoTornillos = "—",
                    Configuracion = "—",
                    TipoAcero = string.IsNullOrWhiteSpace(materialReal) ? "—" : materialReal
                });
            }

            return lista;
        }

        private List<LineaPresupuestoRow> GenerarLineasPresupuesto(int numeroAnillos, int chapasPorAnillo, int anilloArranque)
        {
            var lineas = new List<LineaPresupuestoRow>();
            var resumen = GenerarResumenTanque(numeroAnillos, chapasPorAnillo);

            foreach (var ring in resumen)
            {
                if (!double.TryParse((ring.Espesor ?? "0").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double espesor))
                    espesor = 0;

                double precioUnitario = ObtenerPrecioUnitarioPorEspesorReal(espesor);
                if (precioUnitario <= 0) continue;

                string descripcion = $"{(string.IsNullOrWhiteSpace(ring.TipoAcero) ? "—" : ring.TipoAcero)} - {ring.Altura} mm - Config. {(string.IsNullOrWhiteSpace(ring.Configuracion) ? "—" : ring.Configuracion)} - Espesor {(string.IsNullOrWhiteSpace(ring.Espesor) ? "—" : ring.Espesor)} mm";
                lineas.Add(new LineaPresupuestoRow
                {
                    Cantidad = chapasPorAnillo,
                    Descripcion = descripcion,
                    PrecioUnitario = precioUnitario,
                    Precio = chapasPorAnillo * precioUnitario
                });
            }

            double precioJuegoTornillo = ObtenerPrecioJuegoTornilloBaseReal();
            if (_resultado != null && _resultado.TieneTornilloBase && !string.IsNullOrWhiteSpace(_resultado.NombreTornilloBase) && _resultado.DiametroTornilloBase > 0 && precioJuegoTornillo > 0)
            {
                int totalTornillos = 0;
                if (_resultado.Anillos != null && _resultado.Anillos.Count > 0)
                {
                    foreach (var anillo in _resultado.Anillos)
                    {
                        int verticales = Math.Max(0, anillo.NumeroTornillosVerticales);
                        int horizontales = Math.Max(0, anillo.NumeroTornillosHorizontalesCalculo > 0 ? anillo.NumeroTornillosHorizontalesCalculo : anillo.NumeroTornillosHorizontales);
                        totalTornillos += (verticales + horizontales) * Math.Max(1, chapasPorAnillo);
                    }
                }
                else
                {
                    totalTornillos = (_resultado.NumeroTornillosVerticales + _resultado.NumeroTornillosHorizontales) * Math.Max(1, numeroAnillos) * Math.Max(1, chapasPorAnillo);
                }

                if (totalTornillos > 0)
                {
                    lineas.Add(new LineaPresupuestoRow
                    {
                        Cantidad = totalTornillos,
                        Descripcion = $"Juego tornillo-tuerca-arandela - {_resultado.NombreTornilloBase} - Ø {Formato(_resultado.DiametroTornilloBase, "0.###")} mm",
                        PrecioUnitario = precioJuegoTornillo,
                        Precio = totalTornillos * precioJuegoTornillo
                    });
                }
            }

            if (_resultado != null && _resultado.TieneRigidizadorBase && !string.IsNullOrWhiteSpace(_resultado.NombreRigidizadorBase) && _resultado.PrecioRigidizadorBase > 0)
            {
                lineas.Add(new LineaPresupuestoRow
                {
                    Cantidad = 1,
                    Descripcion = $"Rigidizador base - {_resultado.NombreRigidizadorBase} - h {Formato(_resultado.AlturaRigidizadorBase, "0.###")} mm - e {Formato(_resultado.EspesorRigidizadorBase, "0.###")} mm",
                    PrecioUnitario = _resultado.PrecioRigidizadorBase,
                    Precio = _resultado.PrecioRigidizadorBase
                });
            }

            if (_resultado != null && _resultado.TieneStarterRing && _resultado.AlturaStarterRing > 0)
            {
                int cantidadStarterRing = Math.Max(1, chapasPorAnillo);
                double precioStarterRing = Math.Max(0, _resultado.PrecioStarterRing);

                if (precioStarterRing > 0)
                {
                    lineas.Add(new LineaPresupuestoRow
                    {
                        Cantidad = cantidadStarterRing,
                        Descripcion = $"Starter Ring - h {Formato(_resultado.AlturaStarterRing, "0.###")} mm - F {Formato(_resultado.DistanciaFStarterRing, "0.###")} mm",
                        PrecioUnitario = precioStarterRing,
                        Precio = cantidadStarterRing * precioStarterRing
                    });
                }

                int totalShearKeys = Math.Max(1, _resultado.ShearKeysPorLineaStarterRing * Math.Max(1, chapasPorAnillo));
                double precioShearKey = Math.Max(0, _resultado.PrecioShearKey);

                if (precioShearKey > 0)
                {
                    lineas.Add(new LineaPresupuestoRow
                    {
                        Cantidad = totalShearKeys,
                        Descripcion = $"Shear Keys - {Lang("según cálculo real", "according to real calculation")}",
                        PrecioUnitario = precioShearKey,
                        Precio = totalShearKeys * precioShearKey
                    });
                }
            }
            return lineas;

        }

        private double ObtenerPrecioUnitarioPorEspesorReal(double espesor)
        {
            if (espesor <= 0 || _proyecto == null) return 0;
            var planchas = _calculoTanqueService.ObtenerPlanchasFiltradas(_proyecto);
            if (planchas == null || planchas.Count == 0) return 0;

            foreach (var plancha in planchas)
            {
                if (plancha?.Espesor == null || plancha.Precio == null) continue;

                for (int i = 0; i < plancha.Espesor.Count && i < plancha.Precio.Count; i++)
                {
                    if (Math.Abs(plancha.Espesor[i] - espesor) < 0.0001)
                        return plancha.Precio[i];
                }
            }

            return 0;
        }

        private double ObtenerPrecioJuegoTornilloBaseReal()
        {
            if (_resultado == null || _proyecto == null || _resultado.DiametroTornilloBase <= 0)
                return 0;

            var tornillos = _calculoTanqueService.ObtenerTornillosDisponibles(_proyecto);
            if (tornillos == null || tornillos.Count == 0)
                return 0;

            string nombreBuscado = (_resultado.NombreTornilloBase ?? string.Empty).Trim();
            double diametroBuscado = _resultado.DiametroTornilloBase;

            // 1. Intento exacto: nombre + diámetro
            var coincidenciaExacta = tornillos.FirstOrDefault(t =>
                t != null
                && !string.IsNullOrWhiteSpace(nombreBuscado)
                && (string.Equals((t.CalidadTornillo ?? string.Empty).Trim(), nombreBuscado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals((t.TipoTornillo ?? string.Empty).Trim(), nombreBuscado, StringComparison.OrdinalIgnoreCase))
                && Math.Abs(t.Diametro - diametroBuscado) < 0.2);

            if (coincidenciaExacta != null)
            {
                double precioExacto = ObtenerPrecioJuego(coincidenciaExacta);
                if (precioExacto > 0)
                    return precioExacto;
            }

            // 2. Fallback: por nombre
            var coincidenciaPorNombre = tornillos.FirstOrDefault(t =>
                t != null
                && !string.IsNullOrWhiteSpace(nombreBuscado)
                && (string.Equals((t.CalidadTornillo ?? string.Empty).Trim(), nombreBuscado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals((t.TipoTornillo ?? string.Empty).Trim(), nombreBuscado, StringComparison.OrdinalIgnoreCase)));

            if (coincidenciaPorNombre != null)
            {
                double precioNombre = ObtenerPrecioJuego(coincidenciaPorNombre);
                if (precioNombre > 0)
                    return precioNombre;
            }

            // 3. Fallback: por diámetro cercano
            var coincidenciaPorDiametro = tornillos
                .Where(t => t != null)
                .OrderBy(t => Math.Abs(t.Diametro - diametroBuscado))
                .FirstOrDefault();

            if (coincidenciaPorDiametro != null)
            {
                double precioDiametro = ObtenerPrecioJuego(coincidenciaPorDiametro);
                if (precioDiametro > 0)
                    return precioDiametro;
            }

            return 0;
        }

        private double ObtenerPrecioJuego(PosibleTornilloModel tornillo)
        {
            if (tornillo == null)
                return 0;

            double precioBase = tornillo.Precio != null && tornillo.Precio.Count > 0
                ? tornillo.Precio[0]
                : 0;

            double precioTuerca = tornillo.PrecioTuerca > 0 ? tornillo.PrecioTuerca : 0;
            double precioArandela = tornillo.PrecioArandela > 0 ? tornillo.PrecioArandela : 0;

            return precioBase + precioTuerca + precioArandela;
        }

        private string GenerarTablaHtmlMaterialesPlanchas(int numeroAnillos, int chapasPorAnillo)
        {
            List<ResumenTanqueRow> resumen = GenerarResumenTanque(numeroAnillos, chapasPorAnillo);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Anillo", "Ring")}</th><th>{LangHtml("Cantidad", "Quantity")}</th><th>{LangHtml("Altura (mm)", "Height (mm)")}</th><th>{LangHtml("Espesor (mm)", "Thickness (mm)")}</th><th>{LangHtml("Configuración", "Configuration")}</th><th>{LangHtml("Tipo de acero", "Steel type")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in resumen)
            {
                sb.Append($"<tr><td>{item.Anillo}</td><td>{Math.Max(0, chapasPorAnillo)}</td><td>{Html(item.Altura)}</td><td>{Html(item.Espesor)}</td><td>{Html(item.Configuracion)}</td><td>{Html(item.TipoAcero)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlMaterialesAuxiliares(int numeroAnillos, int chapasPorAnillo)
        {
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Elemento", "Item")}</th><th>{LangHtml("Cantidad", "Quantity")}</th><th>{LangHtml("Descripción", "Description")}</th>");
            sb.Append("</tr></thead><tbody>");

            bool hayFilas = false;

            if (_resultado != null && _resultado.TieneTornilloBase)
            {
                int totalTornillos = 0;

                if (_resultado.Anillos != null && _resultado.Anillos.Count > 0)
                {
                    foreach (var anillo in _resultado.Anillos)
                    {
                        int verticales = Math.Max(0, anillo.NumeroTornillosVerticales);
                        int horizontales = Math.Max(0, anillo.NumeroTornillosHorizontalesCalculo > 0 ? anillo.NumeroTornillosHorizontalesCalculo : anillo.NumeroTornillosHorizontales);
                        totalTornillos += (verticales + horizontales) * Math.Max(1, chapasPorAnillo);
                    }
                }
                else
                {
                    totalTornillos = (_resultado.NumeroTornillosVerticales + _resultado.NumeroTornillosHorizontales) * Math.Max(1, numeroAnillos) * Math.Max(1, chapasPorAnillo);
                }

                sb.Append($"<tr><td>{Html(Lang("Tornillería", "Bolting"))}</td><td>{totalTornillos}</td><td>{Html(TextoSeguroSinInventar(_resultado.NombreTornilloBase))} - Ø {(_resultado.DiametroTornilloBase > 0 ? Formato(_resultado.DiametroTornilloBase, "0.###") + " mm" : "—")}</td></tr>");
                hayFilas = true;
            }

            if (_resultado != null && _resultado.TieneRigidizadorBase)
            {
                sb.Append($"<tr><td>{Html(Lang("Rigidizador base", "Base stiffener"))}</td><td>1</td><td>{Html(TextoSeguroSinInventar(_resultado.NombreRigidizadorBase))} - h {(_resultado.AlturaRigidizadorBase > 0 ? Formato(_resultado.AlturaRigidizadorBase, "0.###") + " mm" : "—")} - e {(_resultado.EspesorRigidizadorBase > 0 ? Formato(_resultado.EspesorRigidizadorBase, "0.###") + " mm" : "—")}</td></tr>");
                hayFilas = true;
            }

            if (_resultado != null && _resultado.TieneStarterRing)
            {
                sb.Append($"<tr><td>Starter Ring</td><td>{Math.Max(1, chapasPorAnillo)}</td><td>h {(_resultado.AlturaStarterRing > 0 ? Formato(_resultado.AlturaStarterRing, "0.###") : "—")} mm - F {(_resultado.DistanciaFStarterRing > 0 ? Formato(_resultado.DistanciaFStarterRing, "0.###") : "—")} mm</td></tr>");
                sb.Append($"<tr><td>Shear Keys</td><td>{Math.Max(1, _resultado.ShearKeysPorLineaStarterRing * Math.Max(1, chapasPorAnillo))}</td><td>{Html(Lang("Shear Keys por línea", "Shear Keys per line"))}: {ValorEntero(_resultado.ShearKeysPorLineaStarterRing)}</td></tr>");
                hayFilas = true;
            }

            if (!hayFilas)
            {
                sb.Append("<tr><td>—</td><td>—</td><td>—</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlResumenTanque(int numeroAnillos, int chapasPorAnillo)
        {
            var lista = GenerarResumenTanque(numeroAnillos, chapasPorAnillo);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Anillo", "Ring")}</th><th>{LangHtml("Altura (mm)", "Height (mm)")}</th><th>{LangHtml("Espesor (mm)", "Thickness (mm)")}</th><th>{LangHtml("Posición rigidizadores", "Wind stiffener position")}</th><th>{LangHtml("Grado tornillos", "Bolt grade")}</th><th>{LangHtml("Configuración", "Configuration")}</th><th>{LangHtml("Tipo de acero", "Steel type")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in lista)
            {
                sb.Append($"<tr><td>{item.Anillo}</td><td>{Html(item.Altura)}</td><td>{Html(item.Espesor)}</td><td>{Html(item.PosicionRigidizadores)}</td><td>{Html(item.GradoTornillos)}</td><td>{Html(item.Configuracion)}</td><td>{Html(item.TipoAcero)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlHidrostatica(int numeroAnillos)
        {
            var lista = GenerarTablaHidrostatica(numeroAnillos);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Anillo", "Ring")}</th><th>{LangHtml("Profundidad (m)", "Depth (m)")}</th><th>{LangHtml("Carga fluido (kN/m)", "Fluid load (kN/m)")}</th><th>{LangHtml("Tensión tracción", "Tensile stress")}</th><th>{LangHtml("Tracción admisible", "Allowable tension")}</th><th>{LangHtml("Aplastamiento", "Bearing stress")}</th><th>{LangHtml("Aplastamiento admisible", "Allowable bearing")}</th><th>{LangHtml("Cortante tornillos", "Bolt shear stress")}</th><th>{LangHtml("Cortante admisible", "Allowable shear")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in lista)
            {
                sb.Append($"<tr><td>{item.Anillo}</td><td>{Html(item.Profundidad)}</td><td>{Html(item.CargaFluido)}</td><td>{Html(item.TensionTraccion)}</td><td>{Html(item.TensionTraccionAdmisible)}</td><td>{Html(item.TensionAgujeros)}</td><td>{Html(item.TensionAgujerosAdmisible)}</td><td>{Html(item.TensionCortanteTornillos)}</td><td>{Html(item.TensionTornillosAdmisible)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlAxial(int numeroAnillos, double factor)
        {
            var lista = GenerarTablaAxial(numeroAnillos, factor);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Anillo", "Ring")}</th><th>{LangHtml("Carga axial (kN/m)", "Axial load (kN/m)")}</th><th>{LangHtml("Tensión axial", "Axial stress")}</th><th>{LangHtml("Axial admisible", "Allowable axial")}</th><th>{LangHtml("Aplastamiento", "Bearing stress")}</th><th>{LangHtml("Aplastamiento admisible", "Allowable bearing")}</th><th>{LangHtml("Cortante tornillos", "Bolt shear")}</th><th>{LangHtml("Cortante admisible", "Allowable shear")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in lista)
            {
                sb.Append($"<tr><td>{item.Anillo}</td><td>{Html(item.CargaAxial)}</td><td>{Html(item.TensionAxial)}</td><td>{Html(item.TensionAxialAdmisible)}</td><td>{Html(item.TensionAgujeros)}</td><td>{Html(item.TensionAgujerosAdmisible)}</td><td>{Html(item.TensionCortanteTornillos)}</td><td>{Html(item.TensionTornillosAdmisible)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlHidrodinamica(int numeroAnillos)
        {
            var lista = GenerarTablaHidrodinamica(numeroAnillos);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Anillo", "Ring")}</th><th>{LangHtml("Carga total (kN/m)", "Total load (kN/m)")}</th><th>{LangHtml("Tensión tracción", "Tensile stress")}</th><th>{LangHtml("Tracción admisible", "Allowable tension")}</th><th>{LangHtml("Aplastamiento", "Bearing stress")}</th><th>{LangHtml("Aplastamiento admisible", "Allowable bearing")}</th><th>{LangHtml("Cortante tornillos", "Bolt shear")}</th><th>{LangHtml("Cortante admisible", "Allowable shear")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in lista)
            {
                sb.Append($"<tr><td>{item.Anillo}</td><td>{Html(item.CargaTotal)}</td><td>{Html(item.TensionTraccion)}</td><td>{Html(item.TensionTraccionAdmisible)}</td><td>{Html(item.TensionAgujeros)}</td><td>{Html(item.TensionAgujerosAdmisible)}</td><td>{Html(item.TensionCortanteTornillos)}</td><td>{Html(item.TensionTornillosAdmisible)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private string GenerarTablaHtmlRigidizadores(int numeroAnillos)
        {
            var lista = GenerarRigidizadores(numeroAnillos);
            var sb = new StringBuilder();
            sb.Append("<table><thead><tr>");
            sb.Append($"<th>{LangHtml("Rigidizador", "Stiffener")}</th><th>{LangHtml("Posición", "Position")}</th><th>{LangHtml("Altura", "Height")}</th><th>{LangHtml("Espesor", "Thickness")}</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var item in lista)
            {
                sb.Append($"<tr><td>{Html(item.Rigidizador)}</td><td>{Html(item.Posicion)}</td><td>{Html(item.ModuloRequerido)}</td><td>{Html(item.ModuloProvisto)}</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private List<TensionHidrostaticaRow> GenerarTablaHidrostatica(int numeroAnillos)
        {
            var lista = new List<TensionHidrostaticaRow>();

            if (_resultado?.Anillos != null && _resultado.Anillos.Count > 0)
            {
                foreach (var anillo in _resultado.Anillos.OrderBy(a => a.NumeroAnillo))
                {
                    lista.Add(new TensionHidrostaticaRow
                    {
                        Anillo = anillo.NumeroAnillo,
                        Profundidad = anillo.Head > 0 ? Formato(anillo.Head, "0.###") : "—",
                        CargaFluido = anillo.HydrostaticHoopLoad > 0 ? Formato(anillo.HydrostaticHoopLoad, "0.###") : "—",
                        TensionTraccion = anillo.NetTensileStress > 0 ? Formato(anillo.NetTensileStress, "0.###") : "—",
                        TensionTraccionAdmisible = anillo.AllowableTensileStress > 0 ? Formato(anillo.AllowableTensileStress, "0.###") : "—",
                        TensionAgujeros = anillo.HoleBearingStress > 0 ? Formato(anillo.HoleBearingStress, "0.###") : "—",
                        TensionAgujerosAdmisible = anillo.AllowableBearingStress > 0 ? Formato(anillo.AllowableBearingStress, "0.###") : "—",
                        TensionCortanteTornillos = anillo.BoltShearStress > 0 ? Formato(anillo.BoltShearStress, "0.###") : "—",
                        TensionTornillosAdmisible = anillo.AllowableShearStress > 0 ? Formato(anillo.AllowableShearStress, "0.###") : "—"
                    });
                }

                return lista;
            }

            for (int i = 1; i <= numeroAnillos; i++)
            {
                lista.Add(new TensionHidrostaticaRow
                {
                    Anillo = i,
                    Profundidad = "—",
                    CargaFluido = "—",
                    TensionTraccion = "—",
                    TensionTraccionAdmisible = "—",
                    TensionAgujeros = "—",
                    TensionAgujerosAdmisible = "—",
                    TensionCortanteTornillos = "—",
                    TensionTornillosAdmisible = "—"
                });
            }

            return lista;
        }

        private List<TensionAxialRow> GenerarTablaAxial(int numeroAnillos, double factor)
        {
            var lista = new List<TensionAxialRow>();

            if (_resultado?.Anillos != null && _resultado.Anillos.Count > 0)
            {
                foreach (var anillo in _resultado.Anillos.OrderBy(a => a.NumeroAnillo))
                {
                    bool esViento = Math.Abs(factor - 1.18) < 0.01;
                    bool esSismo = Math.Abs(factor - 1.42) < 0.01;

                    lista.Add(new TensionAxialRow
                    {
                        Anillo = anillo.NumeroAnillo,
                        CargaAxial = ValorSegunCaso(anillo.AxialLoad, anillo.WindAxialLoad, anillo.SeismicAxialLoad, esViento, esSismo),
                        TensionAxial = ValorSegunCaso(anillo.AxialStress, anillo.WindAxialStress, anillo.SeismicAxialStress, esViento, esSismo),
                        TensionAxialAdmisible = ValorSegunCaso(anillo.AllowableAxialStress, anillo.WindAllowableAxialStress, anillo.SeismicAllowableAxialStress, esViento, esSismo),
                        TensionAgujeros = ValorSegunCaso(anillo.AxialHoleBearingStress, anillo.WindHoleBearingStress, anillo.SeismicHoleBearingStress, esViento, esSismo),
                        TensionAgujerosAdmisible = ValorSegunCaso(anillo.AxialAllowableBearingStress, anillo.WindAllowableBearingStress, anillo.SeismicAllowableBearingStress, esViento, esSismo),
                        TensionCortanteTornillos = ValorSegunCaso(anillo.AxialBoltShearStress, anillo.WindBoltShearStress, anillo.SeismicBoltShearStress, esViento, esSismo),
                        TensionTornillosAdmisible = ValorSegunCaso(anillo.AxialAllowableShearStress, anillo.WindAllowableShearStress, anillo.SeismicAllowableShearStress, esViento, esSismo)
                    });
                }

                return lista;
            }

            for (int i = 1; i <= Math.Max(0, numeroAnillos); i++)
            {
                lista.Add(new TensionAxialRow
                {
                    Anillo = i,
                    CargaAxial = "—",
                    TensionAxial = "—",
                    TensionAxialAdmisible = "—",
                    TensionAgujeros = "—",
                    TensionAgujerosAdmisible = "—",
                    TensionCortanteTornillos = "—",
                    TensionTornillosAdmisible = "—"
                });
            }

            return lista;
        }

        private string ValorSegunCaso(double axial, double wind, double seismic, bool esViento, bool esSismo)
        {
            double valor = esSismo ? seismic : esViento ? wind : axial;
            return valor > 0 ? Formato(valor, "0.###") : "—";
        }

        private List<TensionHidrodinamicaRow> GenerarTablaHidrodinamica(int numeroAnillos)
        {
            var lista = new List<TensionHidrodinamicaRow>();

            if (_resultado?.Anillos != null && _resultado.Anillos.Count > 0)
            {
                foreach (var anillo in _resultado.Anillos.OrderBy(a => a.NumeroAnillo))
                {
                    lista.Add(new TensionHidrodinamicaRow
                    {
                        Anillo = anillo.NumeroAnillo,
                        CargaTotal = anillo.CombinedTotalHoopLoad > 0 ? Formato(anillo.CombinedTotalHoopLoad, "0.###") : "—",
                        TensionTraccion = anillo.CombinedNetTensileStress > 0 ? Formato(anillo.CombinedNetTensileStress, "0.###") : "—",
                        TensionTraccionAdmisible = anillo.CombinedAllowableTensileStress > 0 ? Formato(anillo.CombinedAllowableTensileStress, "0.###") : "—",
                        TensionAgujeros = anillo.CombinedHoleBearingStress > 0 ? Formato(anillo.CombinedHoleBearingStress, "0.###") : "—",
                        TensionAgujerosAdmisible = anillo.CombinedAllowableBearingStress > 0 ? Formato(anillo.CombinedAllowableBearingStress, "0.###") : "—",
                        TensionCortanteTornillos = anillo.CombinedBoltShearStress > 0 ? Formato(anillo.CombinedBoltShearStress, "0.###") : "—",
                        TensionTornillosAdmisible = anillo.CombinedAllowableShearStress > 0 ? Formato(anillo.CombinedAllowableShearStress, "0.###") : "—"
                    });
                }

                return lista;
            }

            for (int i = 1; i <= Math.Max(0, numeroAnillos); i++)
            {
                lista.Add(new TensionHidrodinamicaRow
                {
                    Anillo = i,
                    CargaTotal = "—",
                    TensionTraccion = "—",
                    TensionTraccionAdmisible = "—",
                    TensionAgujeros = "—",
                    TensionAgujerosAdmisible = "—",
                    TensionCortanteTornillos = "—",
                    TensionTornillosAdmisible = "—"
                });
            }

            return lista;
        }

        private List<RigidizadorRow> GenerarRigidizadores(int numeroAnillos)
        {
            var lista = new List<RigidizadorRow>();

            bool rigidizadorRealValido = _resultado != null
                && _resultado.TieneRigidizadorBase
                && !string.IsNullOrWhiteSpace(_resultado.NombreRigidizadorBase)
                && !_resultado.NombreRigidizadorBase.Equals("No encontrado", StringComparison.OrdinalIgnoreCase)
                && _resultado.AlturaRigidizadorBase > 0;

            if (rigidizadorRealValido)
            {
                lista.Add(new RigidizadorRow
                {
                    Rigidizador = _resultado!.NombreRigidizadorBase,
                    Posicion = Lang("Base del tanque", "Tank base"),
                    ModuloRequerido = _resultado.AlturaRigidizadorBase > 0
                        ? Formato(_resultado.AlturaRigidizadorBase, "0.###") + " mm"
                        : "—",
                    ModuloProvisto = _resultado.EspesorRigidizadorBase > 0
                        ? Formato(_resultado.EspesorRigidizadorBase, "0.###") + " mm"
                        : "—"
                });
            }

            if (_resultado != null && _resultado.TieneStarterRing && _resultado.AlturaStarterRing > 0)
            {
                lista.Add(new RigidizadorRow
                {
                    Rigidizador = "Starter Ring",
                    Posicion = Lang("Anillo de arranque", "Starter ring"),
                    ModuloRequerido = Formato(_resultado.AlturaStarterRing, "0.###") + " mm",
                    ModuloProvisto = _resultado.DistanciaFStarterRing > 0
                        ? "F = " + Formato(_resultado.DistanciaFStarterRing, "0.###") + " mm"
                        : "—"
                });
            }

            if (lista.Count == 0)
            {
                lista.Add(new RigidizadorRow
                {
                    Rigidizador = "—",
                    Posicion = Lang("No definido en el cálculo actual", "Not defined in the current calculation"),
                    ModuloRequerido = "—",
                    ModuloProvisto = "—"
                });
            }

            return lista;
        }

        private string DocumentoInicio(string titulo, string subtitulo)
        {
            return $@"<!DOCTYPE html>
<html lang='{CodigoIdiomaHtml()}'>
<head>
<meta charset='utf-8'>
<title>{Html(titulo)}</title>
<style>
@page{{size:A4;margin:16mm 14mm 16mm 14mm;}}
html,body{{font-family:Arial,Helvetica,sans-serif;background:#fff;color:#1f2937;margin:0;padding:0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}}
.wrapper{{max-width:1180px;margin:0 auto;background:#fff;padding:0;}}
.topbar{{display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:18px;}}
.brand-left{{color:#1F3A5F;font-weight:bold;line-height:1.1;}}
.brand-right{{text-align:right;color:#3C8D99;font-size:28px;}}
.muted{{color:#94A3B8;font-size:11px;}}
hr{{border:none;border-top:1px solid #E2E8F0;margin:14px 0 26px 0;}}
.title{{text-align:center;color:#1F3A5F;font-size:34px;font-weight:600;}}
.subtitle{{text-align:center;color:#64748B;font-size:22px;margin-top:10px;}}
.subtitle.small{{font-size:18px;}}
.badges{{text-align:center;margin-top:18px;margin-bottom:20px;}}
.badge{{display:inline-block;background:#EDF4FF;color:#1F2937;border-radius:10px;padding:8px 12px;margin:0 6px 8px 6px;font-size:13px;}}
.notice{{background:#fff;border:1px solid #E2E8F0;padding:14px 16px;border-radius:12px;color:#475569;margin-bottom:24px;}}
.section-title{{color:#1F3A5F;font-size:28px;font-weight:600;margin:8px 0 16px 0;}}
.grid3{{display:grid;grid-template-columns:1fr 1fr 1fr;gap:16px;margin-bottom:18px;}}
.grid2{{display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:18px;}}
.block{{border:1px solid #E2E8F0;border-radius:14px;padding:18px;background:#fff;break-inside:avoid;page-break-inside:avoid;}}
.block h3{{margin:0 0 12px 0;color:#3C8D99;font-size:15px;}}
.label{{color:#64748B;font-size:12.5px;margin-top:6px;}}
.value{{color:#1F2937;font-size:14px;font-weight:600;margin-top:2px;margin-bottom:8px;}}
.table-title{{font-size:16px;font-weight:600;margin:0 0 12px 0;}}
.table-block{{margin-bottom:18px;break-inside:avoid;page-break-inside:avoid;}}
.page-break{{break-before:page;page-break-before:always;}}
table{{width:100%;border-collapse:collapse;margin-bottom:18px;font-size:12.5px;}}
thead{{display:table-header-group;}} tbody{{display:table-row-group;}} tfoot{{display:table-footer-group;}}
tr{{break-inside:avoid;page-break-inside:avoid;}}
th{{background:#F4FAFC;color:#1E293B;text-align:left;padding:9px;border:1px solid #D7E2EA;}}
td{{padding:9px;border:1px solid #EAF0F4;}}
.num{{text-align:right;white-space:nowrap;}}
.multiline{{font-size:13.5px;color:#334155;line-height:1.8;}}
.footer-note{{border:1px solid #E2E8F0;background:#fff;border-radius:12px;padding:16px;margin-top:12px;break-inside:avoid;page-break-inside:avoid;}}
.footer-note h3{{margin:0 0 10px 0;color:#3C8D99;}}
.foot{{margin-top:12px;font-size:12px;color:#94A3B8;}}
@media print{{html,body,.wrapper,.block,.notice,.footer-note{{background:#fff !important;box-shadow:none !important;border-radius:0 !important;}}}}
</style>
</head>
<body><div class='wrapper'>";
        }

        private string TopBar(string codigo, string modeloTanque)
        {
            var sb = new StringBuilder();
            sb.Append("<div class='topbar'>");
            sb.Append("<div class='brand-left'>");
            sb.Append("<div style='font-size:26px;'>TANK</div>");
            sb.Append("<div style='font-size:13px;letter-spacing:2px;'>STRUCTURAL DESIGNER</div>");
            sb.Append("</div>");
            sb.Append($"<div class='muted'>TSD-2026 | {Html(codigo)} | {modeloTanque}</div>");
            sb.Append("<div class='brand-right'>");
            sb.Append("<img src='/assets/images/logo.png' style='max-width:220px; max-height:70px;' alt='Logo' onerror=\"this.style.display='none';\" />");
            sb.Append("</div>");
            sb.Append("</div><hr/>");
            return sb.ToString();
        }

        private string DocumentoFin() => "</div></body></html>";

        private string LabelValue(string label, string value)
        {
            return $"<div class='label'>{Html(label)}</div><div class='value'>{value}</div>";
        }

        private string LineaDatoGlobal(string label, double? valor, string unidad)
        {
            return $"{Html(label)}: {(valor.HasValue && valor.Value > 0 ? Formato(valor.Value, "0.###") + " " + Html(unidad) : "—")}<br/>";
        }

        private string Lang(string es, string en) => EsIngles() ? en : es;
        private string LangHtml(string es, string en) => Html(Lang(es, en));

        private bool EsIngles()
        {
            return string.Equals(_proyecto?.IdiomaInforme, "EN", StringComparison.OrdinalIgnoreCase);
        }

        private string NombreIdiomaVisible()
        {
            return EsIngles() ? "English" : "Español";
        }

        private string CodigoIdiomaHtml()
        {
            return EsIngles() ? "en" : "es";
        }

        private string TextoSeguroSinInventar(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();
        }

        private string ValorNumero(double valor, string formato)
        {
            return valor > 0 ? Formato(valor, formato) : "—";
        }

        private string ValorMm(double valor)
        {
            return valor > 0 ? Formato(valor, "0.###") + " mm" : "—";
        }

        private string ValorKnM2(double valor)
        {
            return valor > 0 ? Formato(valor, "0.##") + " kN/m²" : "—";
        }

        private string ValorEntero(int valor)
        {
            return valor > 0 ? valor.ToString(CultureInfo.InvariantCulture) : "—";
        }

        private string Formato(double valor, string formato)
        {
            var culture = EsIngles() ? CultureInfo.InvariantCulture : new CultureInfo("es-ES");
            return valor.ToString(formato, culture);
        }

        private static string Html(string? texto)
        {
            return WebUtility.HtmlEncode(texto ?? "—");
        }

        private class LineaPresupuestoRow
        {
            public int Cantidad { get; set; }
            public string Descripcion { get; set; } = string.Empty;
            public double PrecioUnitario { get; set; }
            public double Precio { get; set; }
        }

        private class ResumenTanqueRow
        {
            public int Anillo { get; set; }
            public string Altura { get; set; } = "—";
            public string Espesor { get; set; } = "—";
            public string PosicionRigidizadores { get; set; } = "—";
            public string GradoTornillos { get; set; } = "—";
            public string Configuracion { get; set; } = "—";
            public string TipoAcero { get; set; } = "—";
        }

        private class TensionHidrostaticaRow
        {
            public int Anillo { get; set; }
            public string Profundidad { get; set; } = "—";
            public string CargaFluido { get; set; } = "—";
            public string TensionTraccion { get; set; } = "—";
            public string TensionTraccionAdmisible { get; set; } = "—";
            public string TensionAgujeros { get; set; } = "—";
            public string TensionAgujerosAdmisible { get; set; } = "—";
            public string TensionCortanteTornillos { get; set; } = "—";
            public string TensionTornillosAdmisible { get; set; } = "—";
        }

        private class TensionAxialRow
        {
            public int Anillo { get; set; }
            public string CargaAxial { get; set; } = "—";
            public string TensionAxial { get; set; } = "—";
            public string TensionAxialAdmisible { get; set; } = "—";
            public string TensionAgujeros { get; set; } = "—";
            public string TensionAgujerosAdmisible { get; set; } = "—";
            public string TensionCortanteTornillos { get; set; } = "—";
            public string TensionTornillosAdmisible { get; set; } = "—";
        }

        private class TensionHidrodinamicaRow
        {
            public int Anillo { get; set; }
            public string CargaTotal { get; set; } = "—";
            public string TensionTraccion { get; set; } = "—";
            public string TensionTraccionAdmisible { get; set; } = "—";
            public string TensionAgujeros { get; set; } = "—";
            public string TensionAgujerosAdmisible { get; set; } = "—";
            public string TensionCortanteTornillos { get; set; } = "—";
            public string TensionTornillosAdmisible { get; set; } = "—";
        }

        private class RigidizadorRow
        {
            public string Rigidizador { get; set; } = "—";
            public string Posicion { get; set; } = "—";
            public string ModuloRequerido { get; set; } = "—";
            public string ModuloProvisto { get; set; } = "—";
        }
    }
}