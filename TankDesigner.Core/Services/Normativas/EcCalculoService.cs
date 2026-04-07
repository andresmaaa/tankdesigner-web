using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services.Normativas
{
    // Servicio de cálculo para normativa EC.
    // Usa el cálculo base común y queda listo para añadir fórmulas específicas.
    public class EcCalculoService : INormativaCalculoService
    {
        public ResultadoCalculoModel Calcular(CalculoTanqueInputModel input)
        {
            if (input == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No hay datos de entrada para el cálculo EC."
                };
            }

            CalculoTanqueInputModel inputEc = PrepararInput(input);

            var motor = new MotorCalculoService();
            ResultadoCalculoModel resultado = motor.CalcularBase(inputEc, "EC");

            if (resultado == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "No se pudo generar el cálculo EC."
                };
            }

            // Aquí se añadirán más adelante las fórmulas y comprobaciones específicas de EC.
            resultado.Mensaje = resultado.EsValido
                ? "Cálculo generado correctamente con normativa EC."
                : resultado.Mensaje;

            return resultado;
        }

        private CalculoTanqueInputModel PrepararInput(CalculoTanqueInputModel input)
        {
            return new CalculoTanqueInputModel
            {
                Fabricante = input.Fabricante,
                Normativa = "EC",
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