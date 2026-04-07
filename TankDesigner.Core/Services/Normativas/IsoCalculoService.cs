using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Servicio de cálculo para normativa ISO.
    // De momento usa el cálculo base común y queda preparado para ampliar fórmulas después.
    public class IsoCalculoService : INormativaCalculoService
    {
        public ResultadoCalculoModel Calcular(CalculoTanqueInputModel input)
        {
            if (input == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No hay datos de entrada para el cálculo ISO."
                };
            }

            CalculoTanqueInputModel inputIso = PrepararInput(input);

            var motor = new MotorCalculoService();
            ResultadoCalculoModel resultado = motor.CalcularBase(inputIso, "ISO");

            if (resultado == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No se pudo generar el cálculo ISO."
                };
            }

            // Aquí se añadirán más adelante las fórmulas y comprobaciones específicas de ISO.
            resultado.Mensaje = resultado.EsValido
                ? "Cálculo generado correctamente con normativa ISO."
                : resultado.Mensaje;

            return resultado;
        }

        private CalculoTanqueInputModel PrepararInput(CalculoTanqueInputModel input)
        {
            return new CalculoTanqueInputModel
            {
                Fabricante = input.Fabricante,
                Normativa = "ISO",
                MaterialPrincipal = input.MaterialPrincipal,

                ChapasPorAnillo = input.ChapasPorAnillo,
                NumeroAnillos = input.NumeroAnillos,
                AnilloArranque = input.AnilloArranque,

                BordeLibre = input.BordeLibre,
                DensidadLiquido = input.DensidadLiquido,

                Diametro = input.Diametro,
                AlturaTotal = input.AlturaTotal,
                AlturaPanelBase = input.AlturaPanelBase,

                Modelo = input.Modelo,

                NormativaAplicadaCargas = input.NormativaAplicadaCargas,

                VelocidadViento = input.VelocidadViento,
                SnowLoad = input.SnowLoad,

                RoofType = input.RoofType,
                RoofDeadLoad = input.RoofDeadLoad,
                RoofSnowLoad = input.RoofSnowLoad,
                RoofLiveLoad = input.RoofLiveLoad,
                RoofCentroid = input.RoofCentroid,
                RoofProjectedArea = input.RoofProjectedArea,
                RoofAngle = input.RoofAngle,

                ClaseExposicion = input.ClaseExposicion,

                Ss = input.Ss,
                S1 = input.S1,
                TL = input.TL,
                SiteClass = input.SiteClass,
                SeismicUseGroup = input.SeismicUseGroup,

                ObservacionesCargas = input.ObservacionesCargas
            };
        }
    }
}