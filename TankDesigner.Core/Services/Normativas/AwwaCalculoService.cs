using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Servicio de cálculo específico para normativa AWWA.
    // Parte del motor base y después completa los resultados propios de AWWA.
    public class AwwaCalculoService : INormativaCalculoService
    {
        public ResultadoCalculoModel Calcular(CalculoTanqueInputModel input)
        {
            if (input == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No hay datos de entrada para el cálculo AWWA."
                };
            }

            // Prepara el input con la normativa correcta.
            CalculoTanqueInputModel inputAwwa = PrepararInput(input);

            // Ejecuta el cálculo base.
            var motor = new MotorCalculoService();
            ResultadoCalculoModel resultado = motor.CalcularBase(inputAwwa, "AWWA");

            if (resultado == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No se pudo generar el cálculo AWWA."
                };
            }

            // Completa resultados adicionales específicos de AWWA.
            var complementariosService = new AwwaResultadosComplementariosService();
            complementariosService.Completar(inputAwwa, resultado);

            return resultado;
        }

        // Crea una copia del input asegurando que la normativa quede fijada como AWWA.
        private CalculoTanqueInputModel PrepararInput(CalculoTanqueInputModel input)
        {
            return new CalculoTanqueInputModel
            {
                Fabricante = input.Fabricante,
                Normativa = "AWWA",
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

                ObservacionesCargas = input.ObservacionesCargas,

                AwwaWindFactor = input.AwwaWindFactor,
                AwwaRoofFactor = input.AwwaRoofFactor,
                AwwaSeismicFactor = input.AwwaSeismicFactor,
                AwwaGlobalFactor = input.AwwaGlobalFactor
            };
        }
    }
}