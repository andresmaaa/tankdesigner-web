using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de construir el modelo de entrada para el cálculo.
    // Une los datos de proyecto, tanque y cargas en un único objeto.
    public class CalculoInputAdapterService
    {
        private readonly CalculoGeometriaService _calculoGeometriaService;

        public CalculoInputAdapterService()
        {
            _calculoGeometriaService = new CalculoGeometriaService();
        }

        // Construye el input básico del cálculo usando proyecto y tanque.
        public CalculoTanqueInputModel? Construir(ProyectoGeneralModel proyecto, TankModel tanque)
        {
            if (proyecto == null || tanque == null)
                return null;

            double alturaPanelBase = tanque.AlturaPanelBase > 0
                ? tanque.AlturaPanelBase
                : _calculoGeometriaService.ObtenerAlturaPanelBase(tanque, proyecto);

            double alturaTotal = tanque.AlturaTotal > 0
                ? tanque.AlturaTotal
                : _calculoGeometriaService.ObtenerAlturaTotal(tanque, proyecto);

            double diametro = tanque.Diametro > 0
                ? tanque.Diametro
                : _calculoGeometriaService.ObtenerDiametro(tanque, proyecto);

            List<double> alturasAnillos = NormalizarAlturasAnillos(tanque, proyecto, _calculoGeometriaService, alturaPanelBase);
            List<string> materialesAnillos = NormalizarTextosAnillos(
                tanque.MaterialesAnillos,
                tanque.NumeroAnillos,
                string.Empty);
            List<string> configuracionesAnillos = NormalizarTextosAnillos(
                tanque.ConfiguracionesAnillos,
                tanque.NumeroAnillos,
                string.Empty);

            return new CalculoTanqueInputModel
            {
                Fabricante = (proyecto.Fabricante ?? string.Empty).Trim(),
                Normativa = (proyecto.Normativa ?? string.Empty).Trim(),
                MaterialPrincipal = (proyecto.MaterialPrincipal ?? string.Empty).Trim(),

                ChapasPorAnillo = tanque.ChapasPorAnillo,
                NumeroAnillos = tanque.NumeroAnillos,
                AnilloArranque = tanque.AnilloArranque,

                BordeLibre = tanque.BordeLibre,
                DensidadLiquido = tanque.DensidadLiquido,

                Diametro = diametro,
                AlturaTotal = alturaTotal,
                AlturaPanelBase = alturaPanelBase,

                Modelo = (tanque.Modelo ?? string.Empty).Trim(),
                AlturasAnillos = alturasAnillos,
                MaterialesAnillos = materialesAnillos,
                ConfiguracionesAnillos = configuracionesAnillos,
                Anillos = CrearAnillosEntrada(alturasAnillos, materialesAnillos, configuracionesAnillos)
            };
        }

        // Construye el input completo incluyendo las cargas.
        public CalculoTanqueInputModel? Construir(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas)
        {
            var inputBase = Construir(proyecto, tanque);

            if (inputBase == null)
                return null;

            if (cargas == null)
                return inputBase;

            inputBase.NormativaAplicadaCargas = (cargas.NormativaAplicada ?? string.Empty).Trim();

            bool techoNone = string.IsNullOrWhiteSpace(cargas.RoofType)
                             || cargas.RoofType.Trim().Equals("None", StringComparison.OrdinalIgnoreCase);

            inputBase.VelocidadViento = cargas.VelocidadViento;
            inputBase.SnowLoad = techoNone ? 0 : cargas.SnowLoad;

            inputBase.RoofType = techoNone ? "None" : (cargas.RoofType ?? string.Empty).Trim();
            inputBase.RoofDeadLoad = techoNone ? 0 : cargas.RoofDeadLoad;
            inputBase.RoofSnowLoad = techoNone ? 0 : cargas.RoofSnowLoad;
            inputBase.RoofLiveLoad = techoNone ? 0 : cargas.RoofLiveLoad;
            inputBase.RoofCentroid = techoNone ? 0 : cargas.RoofCentroid;
            inputBase.RoofProjectedArea = techoNone ? 0 : cargas.RoofProjectedArea;
            inputBase.RoofAngle = techoNone ? "0°" : (cargas.RoofAngle ?? string.Empty).Trim();

            inputBase.ClaseExposicion = (cargas.ClaseExposicion ?? string.Empty).Trim();

            inputBase.Ss = cargas.Ss;
            inputBase.S1 = cargas.S1;
            inputBase.TL = cargas.TL;
            inputBase.SiteClass = (cargas.SiteClass ?? string.Empty).Trim();
            inputBase.SeismicUseGroup = (cargas.SeismicUseGroup ?? string.Empty).Trim();

            inputBase.ObservacionesCargas = (cargas.Observaciones ?? string.Empty).Trim();

            if (inputBase.DensidadLiquido <= 0 && cargas.DensidadLiquido > 0)
                inputBase.DensidadLiquido = cargas.DensidadLiquido;

            return inputBase;
        }

        private static List<double> NormalizarAlturasAnillos(
            TankModel tanque,
            ProyectoGeneralModel proyecto,
            CalculoGeometriaService calculoGeometriaService,
            double alturaPanelBase)
        {
            var lista = new List<double>();

            if (tanque != null && calculoGeometriaService.AlturasAnillosSonValidasParaCatalogo(tanque, proyecto))
                lista.AddRange(tanque.AlturasAnillos.Where(a => a > 0));

            if (lista.Count == 0 && tanque != null)
                lista.AddRange(calculoGeometriaService.GenerarAlturasAnillosDesdeCatalogo(tanque, proyecto));

            while (lista.Count < Math.Max(0, tanque?.NumeroAnillos ?? 0))
                lista.Add(alturaPanelBase);

            if ((tanque?.NumeroAnillos ?? 0) > 0 && lista.Count > tanque!.NumeroAnillos)
                lista = lista.Take(tanque.NumeroAnillos).ToList();

            return lista;
        }

        private static List<string> NormalizarTextosAnillos(List<string>? listaOriginal, int numeroAnillos, string fallback)
        {
            var lista = new List<string>();

            if (listaOriginal != null)
                lista.AddRange(listaOriginal.Select(x => (x ?? string.Empty).Trim()));

            while (lista.Count < Math.Max(0, numeroAnillos))
                lista.Add(fallback);

            if (numeroAnillos > 0 && lista.Count > numeroAnillos)
                lista = lista.Take(numeroAnillos).ToList();

            return lista;
        }

        private static List<AnilloCalculoModel> CrearAnillosEntrada(
            List<double> alturasAnillos,
            List<string> materialesAnillos,
            List<string> configuracionesAnillos)
        {
            var lista = new List<AnilloCalculoModel>();
            int total = new[] { alturasAnillos?.Count ?? 0, materialesAnillos?.Count ?? 0, configuracionesAnillos?.Count ?? 0 }.Max();

            for (int i = 0; i < total; i++)
            {
                lista.Add(new AnilloCalculoModel
                {
                    NumeroAnillo = i + 1,
                    AlturaMm = alturasAnillos != null && i < alturasAnillos.Count ? alturasAnillos[i] : 0,
                    Material = materialesAnillos != null && i < materialesAnillos.Count ? materialesAnillos[i] : string.Empty,
                    Configuracion = configuracionesAnillos != null && i < configuracionesAnillos.Count ? configuracionesAnillos[i] : string.Empty
                });
            }

            return lista;
        }
    }
}
