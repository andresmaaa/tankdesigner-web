using System.Globalization;
using System.Net;
using System.Text;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;
using TankDesigner.Core.Models.Presupuestos;
namespace TankDesigner.Core.Services
{
    public class InformeHtmlService
    {
        // Servicio principal que me permite consultar catálogos reales
        // de planchas, tornillos y demás elementos que ya existen en el cálculo.

        private readonly CalculoTanqueService _calculoTanqueService = new();
        private readonly JsonCatalogService _jsonCatalogService = new();

        // Contexto del informe actual.
        // Estos campos se cargan una vez con SetContext(...) y luego los reutilizo
        // en todos los métodos auxiliares para no estar pasando los mismos parámetros continuamente.


        private ProyectoGeneralModel? _proyecto;
        private TankModel? _tanque;
        private CargasModel? _cargas;
        private InstalacionModel? _instalacion;
        private ResultadoCalculoModel? _resultado;

        // Genera el informe técnico principal de cálculo estructural.
        // Aquí NO calculo el tanque desde cero, sino que tomo el resultado ya calculado
        // y lo transformo a HTML con formato de informe.
        public string GenerarInformeCalculo(ProyectoGeneralModel proyecto, TankModel tanque, CargasModel cargas, InstalacionModel instalacion, ResultadoCalculoModel resultado)
        {
            // Cargo en memoria el proyecto actual, el tanque, las cargas, la instalación
            // y el resultado de cálculo para que el resto de métodos trabajen sobre ese contexto.
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

            // Conversión de geometría a metros para mostrar datos de resumen del tanque.
            double diametroRealM = DiametroMm() > 0 ? DiametroMm() / 1000.0 : 0;
            double alturaRealM = AlturaTotalMm() > 0 ? AlturaTotalMm() / 1000.0 : 0;
            // Volumen geométrico simple del tanque:
            // V = π · (D/2)^2 · H
            double volumenRealM3 = diametroRealM > 0 && alturaRealM > 0
                ? Math.PI * Math.Pow(diametroRealM / 2.0, 2) * alturaRealM
                : 0;
            // Presión hidrostática base.
            // Si el resultado ya trae la presión calculada, se usa esa.
            // Si no, PresionBase() intenta reconstruirla con densidad y altura.
            double presionBaseMostrar = PresionBase();
            double longitudPlancha = diametroRealM > 0 && chapasPorAnillo > 0
                ? (Math.PI * diametroRealM * 1000.0) / chapasPorAnillo
                : 0;
            double alturaPanelBase = AlturaPanelBaseMm();

            // Comprobaciones para decidir si muestro rigidizador y starter ring
            // como datos reales del cálculo o dejo "—" en el informe.
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

            // A partir de aquí construyo el HTML del informe técnico.
            // Todo lo que se añade a "html" es lo que luego se verá en la vista previa/PDF.
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

        // Genera el informe económico/presupuesto.
        // Aquí junto materiales + instalación + transporte y construyo el resumen económico final.
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

            // Genero todas las líneas económicas de materiales:
            // chapas, tornillería, rigidizador, starter ring, consumibles, techo y transporte.
            List<LineaPresupuestoRow> lineasMaterial = GenerarLineasPresupuesto(numeroAnillos, chapasPorAnillo, anilloArranque);
            // Suma completa del bloque de materiales.
            // Ojo: aquí YA va incluido el transporte porque transporte se mete como línea material.
            double totalMaterial = lineasMaterial.Sum(x => x.Precio);

            // Extraigo el total de transporte de las líneas de materiales para poder mostrarlo
            // separado en el resumen económico final.
            double totalTransporte = ObtenerTotalTransporte(lineasMaterial);
            double totalMaterialSinTransporte = totalMaterial - totalTransporte;
            PresupuestoInstalacionResultadoModel? presupuestoInstalacion = _resultado?.PresupuestoInstalacion;
            // Total de instalación calculado previamente por el servicio de presupuesto de instalación.
            decimal totalInstalacion = presupuestoInstalacion?.TotalInstalacion ?? 0m;
            // Total general del presupuesto:
            // materiales (incluyendo transporte) + instalación.
            decimal totalGeneral = (decimal)totalMaterial + totalInstalacion;

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

            html.Append($"<div class='section-title'>{Html(Lang("Presupuesto de materiales", "Materials budget"))}</div>");
            html.Append("<table><thead><tr>");
            html.Append($"<th>{Html(Lang("Cantidad", "Quantity"))}</th>");
            html.Append($"<th>{Html(Lang("Descripción", "Description"))}</th>");
            html.Append($"<th class='num'>{Html(Lang("Precio unitario", "Unit price"))}</th>");
            html.Append($"<th class='num'>{Html(Lang("Precio", "Price"))}</th>");
            html.Append("</tr></thead><tbody>");

            if (lineasMaterial.Count > 0)
            {
                foreach (var item in lineasMaterial)
                {
                    html.Append("<tr>");
                    html.Append($"<td>{item.Cantidad}</td>");
                    html.Append($"<td>{Html(item.Descripcion)}</td>");
                    html.Append($"<td class='num'>{Formato(item.PrecioUnitario, "0.00")} €</td>");
                    html.Append($"<td class='num'>{Formato(item.Precio, "0.00")} €</td>");
                    html.Append("</tr>");
                }

                html.Append($"<tr><td colspan='3' class='num'><strong>{Html(Lang("Total materiales", "Materials total"))}</strong></td><td class='num'><strong>{Formato(totalMaterial, "0.00")} €</strong></td></tr>");
            }
            else
            {
                html.Append($"<tr><td colspan='4'>{Html(Lang("No hay líneas de presupuesto reales disponibles para los datos actuales.", "There are no real budget lines available for the current data."))}</td></tr>");
            }

            html.Append("</tbody></table>");

            if (presupuestoInstalacion != null && presupuestoInstalacion.Partidas != null && presupuestoInstalacion.Partidas.Count > 0)
            {
                html.Append($"<div class='section-title'>{Html(Lang("Presupuesto de instalación", "Installation budget"))}</div>");

                html.Append("<div class='grid3'>");
                html.Append($"<div class='block'><h3>{Html(Lang("Resumen de instalación", "Installation summary"))}</h3>");
                html.Append($"<p><strong>{Html(Lang("Altura del tanque", "Tank height"))}:</strong> {Formato((double)presupuestoInstalacion.AlturaTanqueMetros, "0.00")} m</p>");
                html.Append($"<p><strong>{Html(Lang("Área de techo", "Roof area"))}:</strong> {Formato((double)presupuestoInstalacion.AreaTechoM2, "0.00")} m²</p>");
                html.Append($"<p><strong>{Html(Lang("Perímetro", "Perimeter"))}:</strong> {Formato((double)presupuestoInstalacion.PerimetroTanqueMetros, "0.00")} m</p>");
                html.Append($"<p><strong>{Html(Lang("Horas totales", "Total hours"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasTotalesGenerales, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Días totales", "Total days"))}:</strong> {Formato((double)presupuestoInstalacion.Calendario.DiasTotalesReales, "0.00")}</p>");
                html.Append("</div>");

                html.Append($"<div class='block'><h3>{Html(Lang("Horas de montaje", "Assembly hours"))}</h3>");
                html.Append($"<p><strong>{Html(Lang("Montaje placas", "Panel assembly"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasMontajePlacas, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Cambios de gato", "Jack changes"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasCambiosGato, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Escaleras", "Ladders"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasEscaleras, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Conexiones", "Connections"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasConexionesYBocaHombre, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Starter ring", "Starter ring"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasStarterRing, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Rigidizadores", "Stiffeners"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasRigidizadores, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Anclaje", "Anchorage"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasAnclaje, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Sellado", "Sealing"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasSelladoCimentacionPared, "0.00")} h</p>");
                html.Append("</div>");

                html.Append($"<div class='block'><h3>{Html(Lang("Techo y calendario", "Roof and schedule"))}</h3>");
                html.Append($"<p><strong>{Html(Lang("Techo estructura", "Roof structure"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasTechoEstructura, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Techo paneles", "Roof panels"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasTechoPaneles, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Descanso", "Rest"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasDescanso, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Desplazamiento", "Travel"))}:</strong> {Formato((double)presupuestoInstalacion.Horas.HorasDesplazamiento, "0.00")} h</p>");
                html.Append($"<p><strong>{Html(Lang("Días techo", "Roof days"))}:</strong> {Formato((double)presupuestoInstalacion.Calendario.DiasTecho, "0.00")}</p>");
                html.Append($"<p><strong>{Html(Lang("Días depósito", "Tank days"))}:</strong> {Formato((double)presupuestoInstalacion.Calendario.DiasDeposito, "0.00")}</p>");
                html.Append($"<p><strong>{Html(Lang("Días Excel", "Excel days"))}:</strong> {Formato((double)presupuestoInstalacion.Calendario.DiasTotalesExcel, "0.00")}</p>");
                html.Append("</div>");
                html.Append("</div>");

                html.Append("<table><thead><tr>");
                html.Append($"<th>{Html(Lang("Código", "Code"))}</th>");
                html.Append($"<th>{Html(Lang("Concepto", "Item"))}</th>");
                html.Append($"<th>{Html(Lang("Cantidad", "Quantity"))}</th>");
                html.Append($"<th>{Html(Lang("Unidad", "Unit"))}</th>");
                html.Append($"<th class='num'>{Html(Lang("Precio unitario", "Unit price"))}</th>");
                html.Append($"<th class='num'>{Html(Lang("Total", "Total"))}</th>");
                html.Append("</tr></thead><tbody>");

                foreach (var partida in presupuestoInstalacion.Partidas)
                {
                    html.Append("<tr>");
                    html.Append($"<td>{Html(partida.Codigo)}</td>");
                    html.Append($"<td>{Html(partida.Concepto)}</td>");
                    html.Append($"<td>{Formato((double)partida.Cantidad, "0.00")}</td>");
                    html.Append($"<td>{Html(partida.Unidad)}</td>");
                    html.Append($"<td class='num'>{Formato((double)partida.PrecioUnitario, "0.00")} €</td>");
                    html.Append($"<td class='num'>{Formato((double)partida.Total, "0.00")} €</td>");
                    html.Append("</tr>");
                }

                html.Append($"<tr><td colspan='5' class='num'><strong>{Html(Lang("Total instalación", "Installation total"))}</strong></td><td class='num'><strong>{Formato((double)totalInstalacion, "0.00")} €</strong></td></tr>");
                html.Append("</tbody></table>");
            }

            html.Append($"<div class='section-title'>{Html(Lang("Resumen económico", "Economic summary"))}</div>");

            html.Append("<table><tbody>");
            html.Append($"<tr><td><strong>{Html(Lang("Materiales sin transporte", "Materials excluding transport"))}</strong></td><td class='num'><strong>{Formato(totalMaterialSinTransporte, "0.00")} €</strong></td></tr>");
            html.Append($"<tr><td><strong>{Html(Lang("Total transporte", "Transport total"))}</strong></td><td class='num'><strong>{Formato(totalTransporte, "0.00")} €</strong></td></tr>");
            html.Append($"<tr><td><strong>{Html(Lang("Total materiales", "Materials total"))}</strong></td><td class='num'><strong>{Formato(totalMaterial, "0.00")} €</strong></td></tr>");
            html.Append($"<tr><td><strong>{Html(Lang("Total instalación", "Installation total"))}</strong></td><td class='num'><strong>{Formato((double)totalInstalacion, "0.00")} €</strong></td></tr>");
            html.Append($"<tr><td><strong>{Html(Lang("TOTAL GENERAL", "GRAND TOTAL"))}</strong></td><td class='num'><strong>{Formato((double)totalGeneral, "0.00")} €</strong></td></tr>");
            html.Append("</tbody></table>");

            html.Append($"<div class='footer-note'><h3>{Html(Lang("Nota", "Note"))}</h3><div class='multiline'>{Html(Lang("Este presupuesto se genera únicamente con cantidades del cálculo actual, precios reales encontrados en catálogo y los cálculos de instalación derivados del estimador Excel integrado. Si no existe dato real suficiente, la línea no se incluye.", "This budget is generated only with quantities from the current calculation, real catalog prices and installation calculations derived from the integrated Excel estimator. If there is not enough real data, the line is not included."))}</div></div>");
            html.Append(DocumentoFin());
            return html.ToString();
        }

        // Genera el listado de materiales.
        // Este informe no es económico: muestra elementos y cantidades/componentes del tanque.
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

        // Guarda el contexto actual del informe en campos privados para que los métodos auxiliares
        // puedan acceder a proyecto, tanque, cargas, instalación y resultado sin repetir parámetros.
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

        // Devuelve la presión hidrostática en base.
        // 1) Si el resultado ya la trae calculada, usa ese valor.
        // 2) Si no, intenta recalcularla con densidad y altura.
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

        // Construye las filas del resumen del tanque anillo a anillo.
        // Aquí se decide qué espesor, configuración, tornillo y rigidizador se muestran por cada anillo.
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

        // Método CLAVE del presupuesto de materiales.
        // Aquí se generan todas las líneas económicas que luego se pintan en el informe:
        // 1) Chapas
        // 2) Tornillería
        // 3) Rigidizador base
        // 4) Starter ring + shear keys
        // 5) Consumibles de virola
        // 6) Techo
        // 7) Transporte
        private List<LineaPresupuestoRow> GenerarLineasPresupuesto(int numeroAnillos, int chapasPorAnillo, int anilloArranque)
        {
            var lineas = new List<LineaPresupuestoRow>();

            if (_resultado == null)
                return lineas;

            string materialReal = !string.IsNullOrWhiteSpace(_resultado.MaterialPrincipal)
                ? _resultado.MaterialPrincipal.Trim()
                : TextoSeguroSinInventar(_proyecto?.MaterialPrincipal);

            // 1) CHAPAS AGRUPADAS POR ESPESOR REAL + CONFIGURACIÓN
            // Agrupo anillos con mismo espesor/altura/configuración para no repetir líneas innecesarias.
            if (_resultado.Anillos != null && _resultado.Anillos.Count > 0)
            {
                var gruposChapas = _resultado.Anillos
                    .OrderBy(x => x.NumeroAnillo)
                    .Select(anillo =>
                    {
                        double espesor = anillo.EspesorSeleccionado > 0
                            ? anillo.EspesorSeleccionado
                            : anillo.EspesorRequerido;

                        double altura = anillo.AlturaSuperior > anillo.AlturaInferior
                            ? anillo.AlturaSuperior - anillo.AlturaInferior
                            : AlturaPanelBaseMm();

                        return new
                        {
                            NumeroAnillo = anillo.NumeroAnillo,
                            Espesor = espesor,
                            Altura = altura,
                            Configuracion = TextoSeguroSinInventar(anillo.ConfiguracionAplicada)
                        };
                    })
                    .Where(x => x.Espesor > 0)
                    .GroupBy(x => new
                    {
                        Espesor = Math.Round(x.Espesor, 3),
                        Altura = Math.Round(x.Altura, 3),
                        Configuracion = x.Configuracion
                    })
                    .OrderBy(g => g.Min(x => x.NumeroAnillo))
                    .ToList();

                foreach (var grupo in gruposChapas)
                {
                    double precioUnitario = ObtenerPrecioUnitarioPorEspesorReal(grupo.Key.Espesor);
                    if (precioUnitario <= 0)
                        continue;

                    double cantidadTotal = grupo.Count() * Math.Max(1, chapasPorAnillo);

                    string anillosTexto = string.Join(", ", grupo
                        .Select(x => x.NumeroAnillo)
                        .OrderBy(x => x));

                    string descripcion =
                        $"{(string.IsNullOrWhiteSpace(materialReal) ? "—" : materialReal)}" +
                        $" - Anillos {anillosTexto}" +
                        $" - Altura {Formato(grupo.Key.Altura, "0.###")} mm" +
                        $" - Espesor {Formato(grupo.Key.Espesor, "0.###")} mm" +
                        $" - Config. {(string.IsNullOrWhiteSpace(grupo.Key.Configuracion) ? "—" : grupo.Key.Configuracion)}";

                    AgregarLineaSiValida(lineas, cantidadTotal, descripcion, precioUnitario);
                }
            }

            // 2) TORNILLERÍA
            // Calcula la cantidad total de juegos tornillo-tuerca-arandela a partir
            // de tornillos verticales + horizontales de cada anillo.
            double precioJuegoTornillo = ObtenerPrecioJuegoTornilloBaseReal();

            if ((!string.IsNullOrWhiteSpace(_resultado.NombreTornilloBase) || _resultado.DiametroTornilloBase > 0)
                && precioJuegoTornillo > 0)
            {
                int totalTornillos = 0;

                if (_resultado.Anillos != null && _resultado.Anillos.Count > 0)
                {
                    foreach (var anillo in _resultado.Anillos)
                    {
                        int verticales = Math.Max(0, anillo.NumeroTornillosVerticales);
                        int horizontales = Math.Max(0,
                            anillo.NumeroTornillosHorizontalesCalculo > 0
                                ? anillo.NumeroTornillosHorizontalesCalculo
                                : anillo.NumeroTornillosHorizontales);

                        totalTornillos += (verticales + horizontales) * Math.Max(1, chapasPorAnillo);
                    }
                }
                else
                {
                    totalTornillos =
                        (_resultado.NumeroTornillosVerticales + _resultado.NumeroTornillosHorizontales)
                        * Math.Max(1, numeroAnillos)
                        * Math.Max(1, chapasPorAnillo);
                }

                if (totalTornillos > 0)
                {
                    AgregarLineaSiValida(
                        lineas,
                        totalTornillos,
                        $"Juego tornillo-tuerca-arandela - {TextoSeguroSinInventar(_resultado.NombreTornilloBase)} - Ø {Formato(_resultado.DiametroTornilloBase, "0.###")} mm",
                        precioJuegoTornillo);
                }
            }

            // 3) RIGIDIZADOR BASE
            // Añade la línea económica del rigidizador base solo si existe en el resultado.
            if (_resultado.TieneRigidizadorBase
                && !string.IsNullOrWhiteSpace(_resultado.NombreRigidizadorBase)
                && _resultado.PrecioRigidizadorBase > 0)
            {
                AgregarLineaSiValida(
                    lineas,
                    1,
                    $"Rigidizador base - {_resultado.NombreRigidizadorBase} - h {Formato(_resultado.AlturaRigidizadorBase, "0.###")} mm - e {Formato(_resultado.EspesorRigidizadorBase, "0.###")} mm",
                    _resultado.PrecioRigidizadorBase);
            }

            // 4) STARTER RING + SHEAR KEYS
            // Añade el starter ring y las shear keys asociadas cuando el cálculo las requiere.
            if (_resultado.TieneStarterRing && _resultado.AlturaStarterRing > 0)
            {
                int cantidadStarterRing = Math.Max(1, chapasPorAnillo);
                double precioStarterRing = Math.Max(0, _resultado.PrecioStarterRing);

                if (precioStarterRing > 0)
                {
                    AgregarLineaSiValida(
                        lineas,
                        cantidadStarterRing,
                        $"Starter Ring - h {Formato(_resultado.AlturaStarterRing, "0.###")} mm - F {Formato(_resultado.DistanciaFStarterRing, "0.###")} mm",
                        precioStarterRing);
                }

                int totalShearKeys = Math.Max(1, _resultado.ShearKeysPorLineaStarterRing * Math.Max(1, chapasPorAnillo));
                double precioShearKey = Math.Max(0, _resultado.PrecioShearKey);

                if (precioShearKey > 0)
                {
                    AgregarLineaSiValida(
                        lineas,
                        totalShearKeys,
                        $"Shear Keys - {Lang("según cálculo real", "according to real calculation")}",
                        precioShearKey);
                }
            }

            // 5) CONSUMIBLES POR PANEL
            // Consumibles asociados a virola/paneles: sellante y pequeños auxiliares.
            double precioConsumiblesPanel = ObtenerPrecioConsumiblesPanel();
            int totalPaneles = Math.Max(0, numeroAnillos) * Math.Max(0, chapasPorAnillo);

            if (precioConsumiblesPanel > 0 && totalPaneles > 0)
            {
                AgregarLineaSiValida(
                    lineas,
                    totalPaneles,
                    Lang("Consumibles de virola (sellante, pequeños auxiliares)", "Shell consumables (sealant, minor auxiliaries)"),
                    precioConsumiblesPanel);
            }

            // 6) SUMINISTRO DE TECHO
            // Si el tanque tiene techo, añado suministro de techo y consumibles de techo.
            string tipoTecho = ObtenerTipoTechoPresupuesto();
            bool tieneTecho = !tipoTecho.Equals("sin techo", StringComparison.OrdinalIgnoreCase);
            double areaTechoM2 = ObtenerAreaTechoM2Presupuesto();

            if (tieneTecho)
            {
                double precioTecho = ObtenerPrecioSuministroTecho();
                if (precioTecho > 0)
                {
                    AgregarLineaSiValida(
                        lineas,
                        1,
                        $"{Lang("Suministro de techo", "Roof supply")} - {TextoSeguroSinInventar(_instalacion?.TipoTecho)}",
                        precioTecho);
                }

                double precioConsumiblesTecho = ObtenerPrecioConsumiblesTechoM2();
                if (precioConsumiblesTecho > 0 && areaTechoM2 > 0)
                {
                    AgregarLineaSiValida(
                        lineas,
                        areaTechoM2,
                        $"{Lang("Consumibles de techo", "Roof consumables")} - {TextoSeguroSinInventar(_instalacion?.TipoTecho)}",
                        precioConsumiblesTecho);
                }
            }

            // 7) TRANSPORTE POR PESO ESTIMADO
            // Estima el peso del suministro, calcula nº de contenedores y añade
            // la línea económica de transporte según ubicación de obra.
            // Si ya existe un transporte manual dentro del presupuesto de instalación,
            // no añado el automático para no duplicar el coste.
            bool tieneTransporteManualInstalacion = TieneTransporteManualInstalacion();
            double pesoTotalKg = ObtenerPesoEstimadoTransporteKg(chapasPorAnillo);
            double pesoMaximoContenedorKg = ObtenerPesoMaximoContenedorKg();
            double precioContenedor = ObtenerPrecioTransportePorUbicacion();

            if (!tieneTransporteManualInstalacion && pesoTotalKg > 0 && pesoMaximoContenedorKg > 0 && precioContenedor > 0)
            {
                double numeroContenedores = Math.Ceiling(pesoTotalKg / pesoMaximoContenedorKg);

                AgregarLineaSiValida(
                    lineas,
                    numeroContenedores,
                    $"{Lang("Transporte de suministro", "Supply transport")} - {TextoSeguroSinInventar(_instalacion?.LugarObra)} - {Formato(pesoTotalKg, "0.##")} kg estimados",
                    precioContenedor);
            }

            return lineas
                .Where(x => x.Cantidad > 0 && x.PrecioUnitario > 0 && x.Precio > 0)
                .OrderBy(x => x.Descripcion)
                .ToList();
        }

        // Añade una línea al presupuesto solo si tiene sentido económico:
        // cantidad > 0 y precio unitario > 0.
        private void AgregarLineaSiValida(List<LineaPresupuestoRow> lineas, double cantidad, string descripcion, double precioUnitario)
        {
            if (cantidad <= 0 || precioUnitario <= 0)
                return;

            lineas.Add(new LineaPresupuestoRow
            {
                Cantidad = cantidad,
                Descripcion = descripcion,
                PrecioUnitario = precioUnitario,
                Precio = cantidad * precioUnitario
            });
        }

        // Carga la configuración de presupuesto/instalación desde JSON
        // según el fabricante del proyecto actual.
        private PresupuestoConfigJsonModel ObtenerConfigPresupuestoActual()
        {
            return _jsonCatalogService.CargarDatosInstalacion(_proyecto?.Fabricante ?? string.Empty);
        }

        private PanelFabricantePresupuestoJsonModel? ObtenerPanelConfigActual()
        {
            var config = ObtenerConfigPresupuestoActual();
            string fabricante = NormalizarClave(_proyecto?.Fabricante);

            return config.PanelesFabricante
                .FirstOrDefault(x => NormalizarClave(x.Fabricante) == fabricante);
        }

        private TechoPresupuestoJsonModel? ObtenerTechoConfigActual()
        {
            var config = ObtenerConfigPresupuestoActual();
            string tipoTecho = NormalizarClave(_instalacion?.TipoTecho);

            return config.Techo
                .FirstOrDefault(x => NormalizarClave(x.Tipo) == tipoTecho);
        }

        private string ObtenerTipoTechoPresupuesto()
        {
            return NormalizarClave(_instalacion?.TipoTecho);
        }

        private string ObtenerUbicacionObraPresupuesto()
        {
            return NormalizarClave(_instalacion?.LugarObra);
        }

        private double ObtenerDiametroMetrosPresupuesto()
        {
            double diametroMm = DiametroMm();
            if (diametroMm > 0)
                return diametroMm / 1000.0;

            return _tanque?.Diametro > 0 ? _tanque.Diametro / 1000.0 : 0;
        }

        private double ObtenerAlturaTanqueMetrosPresupuesto()
        {
            double alturaMm = AlturaTotalMm();
            if (alturaMm > 0)
                return alturaMm / 1000.0;

            return _tanque?.AlturaTotal > 0 ? _tanque.AlturaTotal / 1000.0 : 0;
        }

        private double ObtenerAlturaPanelMetrosPresupuesto()
        {
            double alturaPanelMm = AlturaPanelBaseMm();
            if (alturaPanelMm > 0)
                return alturaPanelMm / 1000.0;

            return _tanque?.AlturaPanelBase > 0 ? _tanque.AlturaPanelBase / 1000.0 : 0;
        }

        private double ObtenerLongitudPanelMetrosPresupuesto()
        {
            var panel = ObtenerPanelConfigActual();
            if (panel != null && panel.LargoPanel > 0)
                return (double)panel.LargoPanel;

            string fabricante = NormalizarClave(_proyecto?.Fabricante);

            if (fabricante.Contains("balmoral"))
                return 2.45;

            if (fabricante.Contains("permastore"))
                return 2.68;

            return 2.68;
        }

        // Calcula el área de techo:
        // 1) Si cargas ya trae área proyectada, usa esa.
        // 2) Si no, la calcula geométricamente con el diámetro del tanque.
        private double ObtenerAreaTechoM2Presupuesto()
        {
            if (_cargas?.RoofProjectedArea > 0)
                return _cargas.RoofProjectedArea;

            double diametroM = ObtenerDiametroMetrosPresupuesto();
            if (diametroM <= 0)
                return 0;

            return Math.PI * Math.Pow(diametroM / 2.0, 2);
        }

        private double ObtenerPrecioConsumiblesPanel()
        {
            var config = ObtenerConfigPresupuestoActual();
            return config.MediosAuxiliares.ConsumiblesPanel > 0
                ? (double)config.MediosAuxiliares.ConsumiblesPanel
                : 2.5;
        }

        private double ObtenerPrecioConsumiblesTechoM2()
        {
            var config = ObtenerConfigPresupuestoActual();
            return config.MediosAuxiliares.ConsumiblesTechoM2 > 0
                ? (double)config.MediosAuxiliares.ConsumiblesTechoM2
                : 3.0;
        }

        private double ObtenerPrecioSuministroTecho()
        {
            var techo = ObtenerTechoConfigActual();
            if (techo != null && techo.PrecioSuministro > 0)
                return (double)techo.PrecioSuministro;

            return 0;
        }

        // Devuelve el precio unitario de transporte según ubicación de obra
        // leyendo el JSON de configuración.
        private double ObtenerPrecioTransportePorUbicacion()
        {
            var config = ObtenerConfigPresupuestoActual();
            string ubicacion = ObtenerUbicacionObraPresupuesto();

            if (ubicacion.Contains("internacional"))
                return config.Transporte.Internacional > 0 ? (double)config.Transporte.Internacional : 6000.0;

            if (ubicacion.Contains("europa"))
                return config.Transporte.Europa > 0 ? (double)config.Transporte.Europa : 4500.0;

            return config.Transporte.Nacional > 0 ? (double)config.Transporte.Nacional : 3600.0;
        }

        private double ObtenerPesoMaximoContenedorKg()
        {
            var config = ObtenerConfigPresupuestoActual();
            return config.Transporte.PesoMaximoContenedor > 0
                ? (double)config.Transporte.PesoMaximoContenedor
                : 20000.0;
        }

        private double ObtenerPesoEscaleraKgMetro()
        {
            var config = ObtenerConfigPresupuestoActual();
            string tipoEscalera = NormalizarClave(_instalacion?.TipoEscalera);

            if (tipoEscalera.Contains("helicoidal"))
                return config.Pesos.EscaleraHelicoidalKgMetro > 0 ? (double)config.Pesos.EscaleraHelicoidalKgMetro : 160.0;

            if (tipoEscalera.Contains("vertical"))
                return config.Pesos.EscaleraVerticalKgMetro > 0 ? (double)config.Pesos.EscaleraVerticalKgMetro : 60.0;

            return 0.0;
        }

        // Estima el peso total transportado del suministro.
        // Usa primero los pesos reales del resultado si existen y, si no,
        // cae al cálculo geométrico por paneles.
        private double ObtenerPesoEstimadoTransporteKg(int chapasPorAnillo)
        {
            var config = ObtenerConfigPresupuestoActual();
            var panel = ObtenerPanelConfigActual();

            double densidadAcero = config.DensidadAcero > 0 ? (double)config.DensidadAcero : 7850.0;
            double alturaPanelM = ObtenerAlturaPanelMetrosPresupuesto();
            double longitudPanelM = panel != null && panel.LargoPanel > 0 ? (double)panel.LargoPanel : ObtenerLongitudPanelMetrosPresupuesto();
            double pesoKgM2Panel = panel != null && panel.PesoKgM2 > 0 ? (double)panel.PesoKgM2 : 40.0;

            double pesoVirolaKg = 0.0;
            double pesoTechoKg = 0.0;

            if (_resultado != null)
            {
                if (_resultado.TankShellDeadLoad > 0)
                    pesoVirolaKg = _resultado.TankShellDeadLoad * 1000.0 / 9.80665;

                if (_resultado.RoofDeadLoad > 0)
                    pesoTechoKg = _resultado.RoofDeadLoad * 1000.0 / 9.80665;
            }

            if (pesoVirolaKg <= 0 && _resultado?.Anillos != null && _resultado.Anillos.Count > 0)
            {
                foreach (var anillo in _resultado.Anillos)
                {
                    double espesorMm = anillo.EspesorSeleccionado > 0
                        ? anillo.EspesorSeleccionado
                        : anillo.EspesorRequerido;

                    if (espesorMm <= 0)
                        continue;

                    double alturaAnilloM = anillo.AlturaSuperior > anillo.AlturaInferior
                        ? (anillo.AlturaSuperior - anillo.AlturaInferior) / 1000.0
                        : alturaPanelM;

                    if (alturaAnilloM <= 0)
                        alturaAnilloM = alturaPanelM;

                    double pesoPanelKg = alturaAnilloM * longitudPanelM * (espesorMm / 1000.0) * densidadAcero;
                    pesoVirolaKg += pesoPanelKg * Math.Max(1, chapasPorAnillo);
                }
            }

            if (pesoTechoKg <= 0 && !ObtenerTipoTechoPresupuesto().Contains("sin techo"))
            {
                double areaTechoM2 = ObtenerAreaTechoM2Presupuesto();
                if (areaTechoM2 > 0)
                    pesoTechoKg = areaTechoM2 * pesoKgM2Panel;
            }

            double pesoEscaleraKg = 0.0;
            if (_instalacion != null && _instalacion.NumeroEscaleras > 0)
            {
                double alturaTanqueM = ObtenerAlturaTanqueMetrosPresupuesto();
                double pesoMetro = ObtenerPesoEscaleraKgMetro();
                pesoEscaleraKg = _instalacion.NumeroEscaleras * alturaTanqueM * pesoMetro;
            }

            double pesoRigidizadorKg = _resultado?.PesoRigidizadorBase ?? 0.0;

            return Math.Max(0, pesoVirolaKg + pesoTechoKg + pesoEscaleraKg + pesoRigidizadorKg);
        }

        private bool TieneTransporteManualInstalacion()
        {
            var presupuestoInstalacion = _resultado?.PresupuestoInstalacion;

            if (presupuestoInstalacion?.Partidas == null || presupuestoInstalacion.Partidas.Count == 0)
                return false;

            return presupuestoInstalacion.Partidas.Any(x =>
                x.Total > 0m &&
                !string.IsNullOrWhiteSpace(x.Concepto) &&
                x.Concepto.Contains("Transporte", StringComparison.OrdinalIgnoreCase));
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
        // Busca en catálogo el precio unitario real de una plancha a partir del espesor.
        // Se usa para construir las líneas de chapas del presupuesto.
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

        // Busca el precio real del juego de tornillería (tornillo + tuerca + arandela).
        // Orden de búsqueda:
        // 1) coincidencia exacta por nombre y diámetro
        // 2) coincidencia por nombre
        // 3) coincidencia por diámetro más cercano
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

        // Devuelve el precio completo de un juego de tornillería:
        // tornillo base + tuerca + arandela.
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

        // Genera la tabla HTML de planchas del listado de materiales.
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

        // Genera la tabla HTML de elementos auxiliares del listado de materiales:
        // tornillería, rigidizador base, starter ring y shear keys.
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

        // Genera la tabla resumen del tanque para el informe técnico.
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

        // Genera la tabla HTML del análisis hidrostático.
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

        // Genera la tabla HTML del análisis axial.
        // El factor se usa para distinguir:
        // 1.00 = axial base
        // 1.18 = axial por viento
        // 1.42 = axial por sismo
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

        // Genera la tabla HTML del análisis hidrodinámico.
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

        // Genera la tabla HTML de rigidizadores/starter ring.
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

        // Construye las filas de datos para la tabla hidrostática.
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

        // Construye las filas de datos para la tabla axial.
        // Decide si usar valores base, de viento o sísmicos según el factor recibido.
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

        // Devuelve el valor formateado según el caso que se esté pintando en la tabla axial:
        // axial normal, axial por viento o axial por sismo.
        private string ValorSegunCaso(double axial, double wind, double seismic, bool esViento, bool esSismo)
        {
            double valor = esSismo ? seismic : esViento ? wind : axial;
            return valor > 0 ? Formato(valor, "0.###") : "—";
        }

        // Construye las filas de datos para la tabla hidrodinámica.
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

        // Construye las filas de rigidizadores para el informe técnico.
        // Si no hay datos reales, devuelve una fila vacía con "—".
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

        // Cabecera HTML general del documento.
        // Aquí está todo el CSS del informe.
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

        // Cabecera visual superior del informe (branding, logo, código de documento).
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

        // Cierre del documento HTML.
        private string DocumentoFin() => "</div></body></html>";

        private string LabelValue(string label, string value)
        {
            return $"<div class='label'>{Html(label)}</div><div class='value'>{value}</div>";
        }

        private string LineaDatoGlobal(string label, double? valor, string unidad)
        {
            return $"{Html(label)}: {(valor.HasValue && valor.Value > 0 ? Formato(valor.Value, "0.###") + " " + Html(unidad) : "—")}<br/>";
        }

        // Suma solo las líneas de transporte dentro del bloque de materiales.
        // Sirve para mostrar el transporte separado en el resumen económico.
        private double ObtenerTotalTransporte(List<LineaPresupuestoRow> lineas)
        {
            if (lineas == null || lineas.Count == 0)
                return 0;

            return lineas
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Descripcion) &&
                    (x.Descripcion.Contains("Transporte de suministro", StringComparison.OrdinalIgnoreCase)
                     || x.Descripcion.Contains("Supply transport", StringComparison.OrdinalIgnoreCase)))
                .Sum(x => x.Precio);
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

        // Fila interna para manejar líneas económicas del presupuesto.
        private class LineaPresupuestoRow
        {
            public double Cantidad { get; set; }
            public string Descripcion { get; set; } = string.Empty;
            public double PrecioUnitario { get; set; }
            public double Precio { get; set; }
        }

        // Fila interna para el resumen del tanque.
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

        // Fila interna para la tabla hidrostática.
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

        // Fila interna para la tabla axial.
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

        // Fila interna para la tabla hidrodinámica.
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

        // Fila interna para la tabla de rigidizadores.
        private class RigidizadorRow
        {
            public string Rigidizador { get; set; } = "—";
            public string Posicion { get; set; } = "—";
            public string ModuloRequerido { get; set; } = "—";
            public string ModuloProvisto { get; set; } = "—";
        }

    }

}