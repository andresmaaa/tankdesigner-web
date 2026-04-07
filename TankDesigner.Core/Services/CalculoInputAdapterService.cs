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
            // Si falta información básica, no se puede construir.
            if (proyecto == null || tanque == null)
                return null;

            // Si el valor ya existe en el tanque se usa, si no se calcula.
            double alturaPanelBase = tanque.AlturaPanelBase > 0
                ? tanque.AlturaPanelBase
                : _calculoGeometriaService.ObtenerAlturaPanelBase(tanque, proyecto);

            double alturaTotal = tanque.AlturaTotal > 0
                ? tanque.AlturaTotal
                : _calculoGeometriaService.ObtenerAlturaTotal(tanque, proyecto);

            double diametro = tanque.Diametro > 0
                ? tanque.Diametro
                : _calculoGeometriaService.ObtenerDiametro(tanque, proyecto);

            // Crea el modelo de entrada con los datos principales.
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

                Modelo = (tanque.Modelo ?? string.Empty).Trim()
            };
        }

        // Construye el input completo incluyendo las cargas.
        public CalculoTanqueInputModel? Construir(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas)
        {
            // Parte del input base.
            var inputBase = Construir(proyecto, tanque);

            if (inputBase == null)
                return null;

            // Si no hay cargas, devuelve solo el input base.
            if (cargas == null)
                return inputBase;

            // Añade datos de cargas al input.
            inputBase.NormativaAplicadaCargas = (cargas.NormativaAplicada ?? string.Empty).Trim();

            inputBase.VelocidadViento = cargas.VelocidadViento;
            inputBase.SnowLoad = cargas.SnowLoad;

            inputBase.RoofType = (cargas.RoofType ?? string.Empty).Trim();
            inputBase.RoofDeadLoad = cargas.RoofDeadLoad;
            inputBase.RoofSnowLoad = cargas.RoofSnowLoad;
            inputBase.RoofLiveLoad = cargas.RoofLiveLoad;
            inputBase.RoofCentroid = cargas.RoofCentroid;
            inputBase.RoofProjectedArea = cargas.RoofProjectedArea;
            inputBase.RoofAngle = (cargas.RoofAngle ?? string.Empty).Trim();

            inputBase.ClaseExposicion = (cargas.ClaseExposicion ?? string.Empty).Trim();

            inputBase.Ss = cargas.Ss;
            inputBase.S1 = cargas.S1;
            inputBase.TL = cargas.TL;
            inputBase.SiteClass = (cargas.SiteClass ?? string.Empty).Trim();
            inputBase.SeismicUseGroup = (cargas.SeismicUseGroup ?? string.Empty).Trim();

            inputBase.ObservacionesCargas = (cargas.Observaciones ?? string.Empty).Trim();

            // Si la densidad no viene en tanque pero sí en cargas, la usa.
            if (inputBase.DensidadLiquido <= 0 && cargas.DensidadLiquido > 0)
                inputBase.DensidadLiquido = cargas.DensidadLiquido;

            return inputBase;
        }
    }
}