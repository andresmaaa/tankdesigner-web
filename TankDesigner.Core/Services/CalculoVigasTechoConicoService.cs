using System.Globalization;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services;

public class CalculoVigasTechoConicoService
{
    public ResultadoVigasTechoConicoModel Calcular(
        TankModel tanque,
        CargasModel cargas,
        ResultadoCalculoModel? resultado = null,
        int? numeroVigasManual = null)
    {
        var tipoTecho = cargas.RoofType ?? string.Empty;

        if (!EsTechoConico(tipoTecho))
        {
            return new ResultadoVigasTechoConicoModel
            {
                Aplica = false,
                TipoTecho = tipoTecho,
                Mensaje = "El cálculo de vigas radiales solo aplica a techos cónicos."
            };
        }

        var diametro = ObtenerValorPositivo(resultado?.Diametro, tanque.Diametro);
        var radio = diametro / 2.0;

        var anguloGrados = ObtenerAnguloTecho(cargas);
        var anguloRad = anguloGrados * Math.PI / 180.0;

        var alturaCono = Math.Tan(anguloRad) * radio;
        var longitudViga = Math.Sqrt(Math.Pow(radio, 2) + Math.Pow(alturaCono, 2));

        var numeroVigas = numeroVigasManual.HasValue && numeroVigasManual.Value > 0
            ? numeroVigasManual.Value
            : CalcularNumeroVigasAproximado(diametro);

        var separacionPerimetral = numeroVigas > 0
            ? Math.PI * diametro / numeroVigas
            : 0;

        var cargaSuperficialTotal =
            Math.Max(0, cargas.RoofDeadLoad) +
            Math.Max(0, cargas.RoofSnowLoad) +
            Math.Max(0, cargas.RoofLiveLoad);

        var areaProyectada = Math.PI * Math.Pow(radio, 2);

        var cargaPorViga = numeroVigas > 0
            ? cargaSuperficialTotal * areaProyectada / numeroVigas
            : 0;

        return new ResultadoVigasTechoConicoModel
        {
            Aplica = true,
            TipoTecho = tipoTecho,
            NumeroVigas = numeroVigas,
            AnguloTechoGrados = anguloGrados,
            DiametroTanque = diametro,
            RadioTanque = radio,
            AlturaCono = Math.Round(alturaCono, 3),
            LongitudViga = Math.Round(longitudViga, 3),
            SeparacionPerimetral = Math.Round(separacionPerimetral, 3),
            CargaSuperficialTotal = Math.Round(cargaSuperficialTotal, 3),
            CargaPorViga = Math.Round(cargaPorViga, 3),
            PerfilSugerido = SugerirPerfil(longitudViga, cargaPorViga),
            Mensaje = "Cálculo aproximado de vigas radiales para visualización y predimensionamiento. No sustituye al cálculo estructural final del techo."
        };
    }

    private static bool EsTechoConico(string? tipoTecho)
    {
        if (string.IsNullOrWhiteSpace(tipoTecho))
            return false;

        var t = tipoTecho.Trim().ToUpperInvariant();

        return t.Contains("CONE")
            || t.Contains("CONIC")
            || t.Contains("CÓNIC")
            || t.Contains("CONO");
    }

    private static double ObtenerValorPositivo(params double?[] valores)
    {
        foreach (var valor in valores)
        {
            if (valor.HasValue && valor.Value > 0)
                return valor.Value;
        }

        return 0;
    }

    private static double ObtenerAnguloTecho(CargasModel cargas)
    {
        var texto = !string.IsNullOrWhiteSpace(cargas.RoofAngle)
            ? cargas.RoofAngle
            : cargas.AnguloSuperior;

        if (string.IsNullOrWhiteSpace(texto))
            return 15;

        texto = texto
            .Replace("°", "")
            .Replace(",", ".")
            .Trim();

        if (double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor) && valor > 0)
            return valor;

        return 15;
    }

    private static int CalcularNumeroVigasAproximado(double diametro)
    {
        if (diametro <= 0)
            return 0;

        if (diametro <= 8) return 12;
        if (diametro <= 14) return 16;
        if (diametro <= 20) return 20;
        if (diametro <= 28) return 24;
        if (diametro <= 36) return 32;

        return 40;
    }

    private static string SugerirPerfil(double longitudViga, double cargaPorViga)
    {
        if (longitudViga <= 0)
            return "Sin datos";

        if (longitudViga <= 4 && cargaPorViga <= 3)
            return "Perfil ligero tipo L / UPN pequeño";

        if (longitudViga <= 7 && cargaPorViga <= 8)
            return "Perfil medio tipo UPN / IPE ligero";

        if (longitudViga <= 11 && cargaPorViga <= 15)
            return "Perfil medio-reforzado tipo IPE / HEA ligero";

        return "Perfil reforzado. Requiere verificación específica del techo.";
    }
}