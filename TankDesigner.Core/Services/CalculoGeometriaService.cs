using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de calcular la geometría básica del tanque.
    // Toma como referencia las dimensiones reales disponibles en los JSON del fabricante.
    public class CalculoGeometriaService
    {
        private const double AlturaFallbackMm = 1200.0;

        private readonly CalculoTanqueService _calculoTanqueService;

        public CalculoGeometriaService()
        {
            _calculoTanqueService = new CalculoTanqueService();
        }

        private List<PosiblePlanchaModel> ObtenerPlanchasValidas(ProyectoGeneralModel proyecto)
        {
            if (proyecto == null)
                return new List<PosiblePlanchaModel>();

            return _calculoTanqueService.ObtenerPlanchasFiltradas(proyecto)
                .Where(p => p != null && p.Altura > 0 && p.Ancho > 0)
                .ToList();
        }

        // La altura de referencia no se fuerza a 1200.
        // Se obtiene del JSON usando la altura de panel más representativa del catálogo activo.
        private PosiblePlanchaModel? ObtenerPlanchaReferencia(ProyectoGeneralModel proyecto)
        {
            var planchas = ObtenerPlanchasValidas(proyecto);

            if (planchas.Count == 0)
                return null;

            double alturaReferencia = planchas
                .GroupBy(p => p.Altura)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .First()
                .Key;

            return planchas
                .Where(p => Math.Abs(p.Altura - alturaReferencia) < 0.001)
                .OrderBy(p => p.Ancho)
                .ThenBy(p => p.Fy)
                .ThenBy(p => p.Fu)
                .FirstOrDefault();
        }

        public double ObtenerAlturaPanelBase(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            var plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha != null && plancha.Altura > 0)
                return plancha.Altura;

            if (tanque.AlturaPanelBase > 0)
                return tanque.AlturaPanelBase;

            return AlturaFallbackMm;
        }

        public double ObtenerAlturaTotal(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            var alturasCatalogo = GenerarAlturasAnillosDesdeCatalogo(tanque, proyecto);

            if (alturasCatalogo.Count > 0)
                return alturasCatalogo.Sum();

            double alturaPanel = ObtenerAlturaPanelBase(tanque, proyecto);

            if (alturaPanel <= 0 || tanque.NumeroAnillos <= 0)
                return 0;

            return tanque.NumeroAnillos * alturaPanel;
        }

        public double ObtenerDiametro(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return 0;

            PosiblePlanchaModel? plancha = ObtenerPlanchaReferencia(proyecto);

            if (plancha == null || plancha.Ancho <= 0 || tanque.ChapasPorAnillo <= 0)
                return 0;

            return (tanque.ChapasPorAnillo * plancha.Ancho) / Math.PI;
        }

        public List<double> GenerarAlturasAnillosDesdeCatalogo(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            var resultado = new List<double>();

            if (tanque == null || proyecto == null || tanque.NumeroAnillos <= 0)
                return resultado;

            var planchas = ObtenerPlanchasValidas(proyecto);
            if (planchas.Count == 0)
                return resultado;

            double alturaPanelBase = ObtenerAlturaPanelBase(tanque, proyecto);
            if (alturaPanelBase <= 0)
                return resultado;

            double alturaObjetivo = tanque.AlturaTotal > 0 ? tanque.AlturaTotal : 0;

            if (alturaObjetivo <= 0)
            {
                resultado.AddRange(Enumerable.Repeat(alturaPanelBase, tanque.NumeroAnillos));
                return resultado;
            }

            double alturaCompleta = alturaPanelBase * tanque.NumeroAnillos;

            if (Math.Abs(alturaObjetivo - alturaCompleta) < 0.001)
            {
                resultado.AddRange(Enumerable.Repeat(alturaPanelBase, tanque.NumeroAnillos));
                return resultado;
            }

            var alturasParcialesCatalogo = planchas
                .Select(p => p.Altura)
                .Where(a => a > 0 && a < alturaPanelBase)
                .Distinct()
                .OrderByDescending(a => a)
                .ToList();

            if (tanque.NumeroAnillos >= 2 && alturasParcialesCatalogo.Count > 0)
            {
                foreach (double alturaInferior in alturasParcialesCatalogo)
                {
                    double alturaSuperior = alturaObjetivo - alturaInferior - ((tanque.NumeroAnillos - 2) * alturaPanelBase);

                    if (alturaSuperior > 0 && alturaSuperior <= alturaPanelBase)
                    {
                        resultado.Add(alturaInferior);

                        for (int i = 0; i < tanque.NumeroAnillos - 2; i++)
                            resultado.Add(alturaPanelBase);

                        resultado.Add(Math.Round(alturaSuperior, 3));
                        return resultado;
                    }
                }
            }

            double restante = alturaObjetivo;

            for (int i = 0; i < tanque.NumeroAnillos; i++)
            {
                int anillosRestantes = tanque.NumeroAnillos - i - 1;
                double alturaActual = Math.Min(alturaPanelBase, restante - (anillosRestantes * alturaPanelBase));

                if (alturaActual <= 0 || alturaActual > alturaPanelBase)
                    alturaActual = alturaPanelBase;

                resultado.Add(Math.Round(alturaActual, 3));
                restante -= alturaActual;
            }

            return resultado;
        }

        public bool AlturasAnillosSonValidasParaCatalogo(TankModel tanque, ProyectoGeneralModel proyecto)
        {
            if (tanque == null || proyecto == null)
                return false;

            if (tanque.AlturasAnillos == null || tanque.AlturasAnillos.Count != tanque.NumeroAnillos)
                return false;

            if (tanque.AlturasAnillos.Any(a => a <= 0))
                return false;

            double alturaPanelBaseCatalogo = ObtenerAlturaPanelBase(tanque, proyecto);

            if (!NormalizarFabricante(proyecto.Fabricante).Equals("BALMORAL", StringComparison.OrdinalIgnoreCase)
                && tanque.AlturasAnillos.All(a => Math.Abs(a - 1200.0) < 0.001)
                && Math.Abs(alturaPanelBaseCatalogo - 1200.0) > 0.001)
            {
                return false;
            }

            return true;
        }

        private static string NormalizarFabricante(string? fabricante)
        {
            if (string.IsNullOrWhiteSpace(fabricante))
                return string.Empty;

            return fabricante.Trim().ToUpperInvariant();
        }
    }
}
