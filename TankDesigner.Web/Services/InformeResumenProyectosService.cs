using System.Globalization;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using TankDesigner.Core.Models;
using TankDesigner.Core.Services;
using TankDesigner.Web.Data;

namespace TankDesigner.Web.Services
{
    public class InformeResumenProyectosService
    {
        private readonly InformeHtmlService _informeHtmlService;

        public InformeResumenProyectosService(InformeHtmlService informeHtmlService)
        {
            _informeHtmlService = informeHtmlService;
        }

        public string GenerarInformeResumenPresupuestos(IEnumerable<ProyectoEntidad> proyectosSeleccionados)
        {
            var proyectos = (proyectosSeleccionados ?? Enumerable.Empty<ProyectoEntidad>())
                .OrderByDescending(x => x.FechaModificacion)
                .ToList();

            var filas = new List<FilaResumenProyecto>();

            foreach (var entidad in proyectos)
            {
                var proyecto = DeserializarSeguro<ProyectoGeneralModel>(entidad.ProyectoJson);
                var tanque = DeserializarSeguro<TankModel>(entidad.TanqueJson);
                var cargas = DeserializarSeguro<CargasModel>(entidad.CargasJson);
                var instalacion = DeserializarSeguro<InstalacionModel>(entidad.InstalacionJson);
                var resultado = DeserializarSeguro<ResultadoCalculoModel>(entidad.ResultadoJson);

                var resumen = _informeHtmlService.ObtenerResumenEconomicoPresupuesto(
                    proyecto,
                    tanque,
                    cargas,
                    instalacion,
                    resultado);

                filas.Add(new FilaResumenProyecto
                {
                    Nombre = ValorSeguro(entidad.Nombre, "Proyecto sin nombre"),
                    Cliente = ValorSeguro(entidad.Cliente),
                    Normativa = ValorSeguro(entidad.Normativa),
                    Fabricante = ValorSeguro(entidad.Fabricante),
                    FechaModificacion = entidad.FechaModificacion,
                    Materiales = resumen.TotalMateriales,
                    Instalacion = resumen.TotalInstalacion,
                    Transporte = resumen.TotalTransporte,
                    Total = resumen.TotalGeneral,
                    NumeroAnillos = tanque.NumeroAnillos > 0 ? tanque.NumeroAnillos : resultado.NumeroAnillos,
                    ChapasPorAnillo = tanque.ChapasPorAnillo > 0 ? tanque.ChapasPorAnillo : resultado.ChapasPorAnillo,
                    Diametro = tanque.Diametro > 0 ? tanque.Diametro : resultado.Diametro,
                    Altura = tanque.AlturaTotal > 0 ? tanque.AlturaTotal : resultado.AlturaTotal,
                    TieneStarterRing = instalacion.StarterRing || resultado.TieneStarterRing,
                    TipoTecho = ValorSeguro(instalacion.TipoTecho),
                    LugarObra = ValorSeguro(instalacion.LugarObra),
                    LineasMaterial = resumen.NumeroLineasMaterial,
                    PartidasInstalacion = resumen.NumeroPartidasInstalacion
                });
            }

            var totalMateriales = filas.Sum(x => x.Materiales);
            var totalInstalacion = filas.Sum(x => x.Instalacion);
            var totalTransporte = filas.Sum(x => x.Transporte);
            var totalGeneral = filas.Sum(x => x.Total);

            var html = new StringBuilder();
            html.Append("""
<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8'>
<title>Resumen global de presupuestos</title>
<style>
@page{size:A4;margin:16mm 14mm 16mm 14mm;}
html,body{font-family:Arial,Helvetica,sans-serif;background:#fff;color:#1f2937;margin:0;padding:0;-webkit-print-color-adjust:exact;print-color-adjust:exact;}
body{font-size:12.5px;}
.wrapper{max-width:1180px;margin:0 auto;background:#fff;padding:0;}
.topbar{display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-bottom:18px;}
.brand-left{color:#1F3A5F;font-weight:bold;line-height:1.1;}
.brand-right{text-align:right;color:#3C8D99;font-size:28px;}
.muted{color:#94A3B8;font-size:11px;}
hr{border:none;border-top:1px solid #E2E8F0;margin:14px 0 26px 0;}
.title{text-align:center;color:#1F3A5F;font-size:34px;font-weight:600;}
.subtitle{text-align:center;color:#64748B;font-size:18px;margin-top:10px;}
.badges{text-align:center;margin-top:18px;margin-bottom:20px;}
.badge{display:inline-block;background:#EDF4FF;color:#1F2937;border-radius:10px;padding:8px 12px;margin:0 6px 8px 6px;font-size:13px;}
.notice{background:#fff;border:1px solid #E2E8F0;padding:14px 16px;border-radius:12px;color:#475569;margin-bottom:24px;}
.section-title{color:#1F3A5F;font-size:28px;font-weight:600;margin:8px 0 16px 0;}
.grid4{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:24px;}
.kpi-card{border:1px solid #E2E8F0;border-radius:14px;padding:16px;background:#fff;break-inside:avoid;page-break-inside:avoid;}
.kpi-label{display:block;font-size:12px;color:#64748B;margin-bottom:8px;}
.kpi-value{display:block;font-size:23px;font-weight:700;color:#1F3A5F;}
.kpi-sub{display:block;font-size:12px;color:#94A3B8;margin-top:6px;}
.project-block{border:1px solid #E2E8F0;border-radius:16px;padding:18px;background:#fff;margin-bottom:18px;break-inside:avoid;page-break-inside:avoid;}
.project-head{display:flex;justify-content:space-between;gap:20px;align-items:flex-start;margin-bottom:14px;}
.project-title{font-size:20px;color:#1F3A5F;font-weight:700;margin:0;}
.project-date{font-size:12px;color:#94A3B8;white-space:nowrap;}
.meta-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:14px;}
.meta-item{background:#F8FBFD;border:1px solid #E2E8F0;border-radius:12px;padding:12px;}
.meta-label{display:block;font-size:11px;color:#64748B;margin-bottom:6px;}
.meta-value{display:block;font-size:13px;color:#1F2937;font-weight:600;}
.cost-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;}
.cost-card{border:1px solid #D7E2EA;border-radius:12px;padding:14px;background:#fff;}
.cost-card--total{background:#1F3A5F;color:#fff;border-color:#1F3A5F;}
.cost-card--total .cost-label,.cost-card--total .cost-sub{color:#DBEAFE;}
.cost-card--total .cost-value{color:#fff;}
.cost-label{display:block;font-size:11px;color:#64748B;margin-bottom:8px;text-transform:uppercase;letter-spacing:.06em;}
.cost-value{display:block;font-size:21px;font-weight:700;color:#1F3A5F;}
.cost-sub{display:block;font-size:11px;color:#94A3B8;margin-top:6px;}
.table-shell{margin-top:14px;}
table{width:100%;border-collapse:collapse;font-size:12.5px;}
thead{display:table-header-group;}
tr{break-inside:avoid;page-break-inside:avoid;}
th{background:#F4FAFC;color:#1E293B;text-align:left;padding:9px;border:1px solid #D7E2EA;}
td{padding:9px;border:1px solid #EAF0F4;}
.num{text-align:right;white-space:nowrap;}
.page-break{break-before:page;page-break-before:always;}
.footer-note{border:1px solid #E2E8F0;background:#fff;border-radius:12px;padding:16px;margin-top:16px;}
.footer-note h3{margin:0 0 10px 0;color:#3C8D99;}
@media print{html,body,.wrapper,.project-block,.kpi-card,.notice,.footer-note{background:#fff !important;box-shadow:none !important;}}
@media screen and (max-width: 960px){.grid4,.meta-grid,.cost-grid{grid-template-columns:1fr 1fr;}}
</style>
</head>
<body>
<div class='wrapper'>
""");

            html.Append("<div class='topbar'>");
            html.Append("<div class='brand-left'><div style='font-size:26px;'>TANK</div><div style='font-size:13px;letter-spacing:2px;'>STRUCTURAL DESIGNER</div></div>");
            html.Append($"<div class='muted'>TSD-2026 | Resumen global de presupuestos | {DateTime.Now.ToString("dd/MM/yyyy HH:mm", new CultureInfo("es-ES"))}</div>");
            html.Append("<div class='brand-right'><img src='/assets/images/logo.png' style='max-width:220px; max-height:70px;' alt='Logo' onerror=\"this.style.display='none';\" /></div>");
            html.Append("</div><hr/>");

            html.Append("<div class='title'>Resumen global de presupuestos</div>");
            html.Append("<div class='subtitle'>Informe consolidado de proyectos seleccionados</div>");
            html.Append("<div class='badges'>");
            html.Append($"<span class='badge'>Proyectos incluidos: {filas.Count}</span>");
            html.Append($"<span class='badge'>Materiales: {Formato(totalMateriales)}</span>");
            html.Append($"<span class='badge'>Instalación: {Formato(totalInstalacion)}</span>");
            html.Append($"<span class='badge'>Total global: {Formato(totalGeneral)}</span>");
            html.Append("</div>");
            html.Append("""
<div class='notice'>
Este documento resume los presupuestos de los proyectos seleccionados en el centro de gestión.
Se muestran los importes reales calculados actualmente para materiales, transporte e instalación,
junto con un consolidado económico final listo para revisión, impresión o entrega interna.
</div>
""");
            html.Append("<div class='section-title'>Consolidado económico</div>");
            html.Append("<div class='grid4'>");
            html.Append(Kpi("Total materiales", Formato(totalMateriales), "Suma global del suministro"));
            html.Append(Kpi("Total instalación", Formato(totalInstalacion), "Montaje y medios de obra"));
            html.Append(Kpi("Total transporte", Formato(totalTransporte), "Incluido dentro de materiales"));
            html.Append(Kpi("Total global", Formato(totalGeneral), "Materiales + instalación"));
            html.Append("</div>");
            html.Append("<div class='table-shell'><table><thead><tr>");
            html.Append("<th>Proyecto</th><th>Cliente</th><th>Normativa</th><th>Fabricante</th><th class='num'>Materiales</th><th class='num'>Instalación</th><th class='num'>Total</th>");
            html.Append("</tr></thead><tbody>");
            foreach (var fila in filas)
            {
                html.Append("<tr>");
                html.Append($"<td><strong>{Html(fila.Nombre)}</strong></td>");
                html.Append($"<td>{Html(fila.Cliente)}</td>");
                html.Append($"<td>{Html(fila.Normativa)}</td>");
                html.Append($"<td>{Html(fila.Fabricante)}</td>");
                html.Append($"<td class='num'>{Formato(fila.Materiales)}</td>");
                html.Append($"<td class='num'>{Formato(fila.Instalacion)}</td>");
                html.Append($"<td class='num'><strong>{Formato(fila.Total)}</strong></td>");
                html.Append("</tr>");
            }
            html.Append("</tbody><tfoot><tr>");
            html.Append("<td colspan='4' class='num'><strong>Total consolidado</strong></td>");
            html.Append($"<td class='num'><strong>{Formato(totalMateriales)}</strong></td>");
            html.Append($"<td class='num'><strong>{Formato(totalInstalacion)}</strong></td>");
            html.Append($"<td class='num'><strong>{Formato(totalGeneral)}</strong></td>");
            html.Append("</tr></tfoot></table></div>");
            html.Append("<div class='page-break'></div><div class='section-title'>Detalle por proyecto</div>");
            foreach (var fila in filas)
            {
                html.Append("<section class='project-block'>");
                html.Append("<div class='project-head'>");
                html.Append($"<div><h2 class='project-title'>{Html(fila.Nombre)}</h2></div>");
                html.Append($"<div class='project-date'>Actualizado: {fila.FechaModificacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm", new CultureInfo("es-ES"))}</div>");
                html.Append("</div>");
                html.Append("<div class='meta-grid'>");
                html.Append(Meta("Cliente", fila.Cliente));
                html.Append(Meta("Normativa", fila.Normativa));
                html.Append(Meta("Fabricante", fila.Fabricante));
                html.Append(Meta("Ubicación obra", fila.LugarObra));
                html.Append(Meta("Nº anillos", ValorEntero(fila.NumeroAnillos)));
                html.Append(Meta("Chapas por anillo", ValorEntero(fila.ChapasPorAnillo)));
                html.Append(Meta("Diámetro", ValorMetro(fila.Diametro)));
                html.Append(Meta("Altura", ValorMetro(fila.Altura)));
                html.Append(Meta("Starter ring", fila.TieneStarterRing ? "Sí" : "No"));
                html.Append(Meta("Tipo de techo", fila.TipoTecho));
                html.Append(Meta("Líneas materiales", ValorEntero(fila.LineasMaterial)));
                html.Append(Meta("Partidas instalación", ValorEntero(fila.PartidasInstalacion)));
                html.Append("</div>");
                html.Append("<div class='cost-grid'>");
                html.Append(Coste("Materiales", Formato(fila.Materiales), "Suministro total calculado"));
                html.Append(Coste("Instalación", Formato(fila.Instalacion), "Montaje, viajes y medios"));
                html.Append(Coste("Transporte", Formato(fila.Transporte), "Incluido en materiales"));
                html.Append(CosteTotal("Total proyecto", Formato(fila.Total), "Coste consolidado actual"));
                html.Append("</div>");
                html.Append("</section>");
            }
            html.Append("""
<div class='footer-note'>
<h3>Observación</h3>
<p>
Los importes de este informe resumen dependen directamente del estado guardado de cada proyecto.
Si se recalcula un proyecto, el resumen consolidado también cambiará cuando se vuelva a generar.
</p>
</div>
</div>
</body>
</html>
""");

            return html.ToString();
        }

        private static T DeserializarSeguro<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json))
                return new T();

            try
            {
                return JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        private static string ValorSeguro(string? valor, string fallback = "—")
        {
            return string.IsNullOrWhiteSpace(valor) ? fallback : valor.Trim();
        }

        private static string Html(string? valor) => WebUtility.HtmlEncode(ValorSeguro(valor));
        private static string ValorEntero(int valor) => valor > 0 ? valor.ToString(CultureInfo.InvariantCulture) : "—";
        private static string ValorMetro(double valor) => valor > 0 ? valor.ToString("0.###", new CultureInfo("es-ES")) + " m" : "—";
        private static string Formato(double valor) => valor.ToString("0.00", new CultureInfo("es-ES")) + " €";
        private static string Kpi(string label, string value, string subtexto) => $"<div class='kpi-card'><span class='kpi-label'>{Html(label)}</span><span class='kpi-value'>{Html(value)}</span><span class='kpi-sub'>{Html(subtexto)}</span></div>";
        private static string Meta(string label, string value) => $"<div class='meta-item'><span class='meta-label'>{Html(label)}</span><span class='meta-value'>{Html(value)}</span></div>";
        private static string Coste(string label, string value, string subtexto) => $"<div class='cost-card'><span class='cost-label'>{Html(label)}</span><span class='cost-value'>{Html(value)}</span><span class='cost-sub'>{Html(subtexto)}</span></div>";
        private static string CosteTotal(string label, string value, string subtexto) => $"<div class='cost-card cost-card--total'><span class='cost-label'>{Html(label)}</span><span class='cost-value'>{Html(value)}</span><span class='cost-sub'>{Html(subtexto)}</span></div>";

        private sealed class FilaResumenProyecto
        {
            public string Nombre { get; set; } = "Proyecto sin nombre";
            public string Cliente { get; set; } = "—";
            public string Normativa { get; set; } = "—";
            public string Fabricante { get; set; } = "—";
            public DateTime FechaModificacion { get; set; }
            public double Materiales { get; set; }
            public double Instalacion { get; set; }
            public double Transporte { get; set; }
            public double Total { get; set; }
            public int NumeroAnillos { get; set; }
            public int ChapasPorAnillo { get; set; }
            public double Diametro { get; set; }
            public double Altura { get; set; }
            public bool TieneStarterRing { get; set; }
            public string TipoTecho { get; set; } = "—";
            public string LugarObra { get; set; } = "—";
            public int LineasMaterial { get; set; }
            public int PartidasInstalacion { get; set; }
        }
    }
}
