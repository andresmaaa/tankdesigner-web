using TankDesigner.Core.Interfaces;
using TankDesigner.Core.Models;
using TankDesigner.Core.Models.Catalogos;
using TankDesigner.Core.Services.Normativas;

namespace TankDesigner.Core.Services
{
    // Motor principal del cálculo.
    // Valida la entrada, selecciona la normativa y monta el resultado final.
    public class MotorCalculoService
    {
        private readonly NormativaCalculoSelectorService _selectorService;

        public MotorCalculoService()
        {
            _selectorService = new NormativaCalculoSelectorService();
        }

        // Método principal de entrada.
        // Valida el input y delega el cálculo al servicio de normativa correspondiente.
        public ResultadoCalculoModel Calcular(CalculoTanqueInputModel input)
        {
            string errorValidacion = ValidarInputBasico(input);
            if (!string.IsNullOrWhiteSpace(errorValidacion))
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = errorValidacion
                };
            }

            INormativaCalculoService servicioNormativa = _selectorService.ObtenerServicio(input.Normativa);
            if (servicioNormativa == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = $"No existe un servicio de cálculo para la normativa '{input.Normativa}'."
                };
            }

            ResultadoCalculoModel resultado = servicioNormativa.Calcular(input);

            if (resultado == null)
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = "El servicio de cálculo no devolvió ningún resultado."
                };
            }

            return resultado;
        }

        // Cálculo base común para todas las normativas.
        // Aquí solo va la parte general, no la lógica específica de AWWA, ISO o EC.
        public ResultadoCalculoModel CalcularBase(CalculoTanqueInputModel input, string normativaAplicada)
        {
            string errorValidacion = ValidarInputBasico(input);
            if (!string.IsNullOrWhiteSpace(errorValidacion))
            {
                return new ResultadoCalculoModel
                {
                    EsValido = false,
                    Mensaje = errorValidacion
                };
            }

            ResultadoCalculoModel resultado = CrearResultadoBase(input, normativaAplicada);

            PosibleConfiguracionModel configuracionBase = ObtenerConfiguracion(input);
            AplicarConfiguracion(resultado, configuracionBase);

            CalcularAnillos(input, resultado, normativaAplicada);
            AplicarSeleccionRealDesdeAnillos(resultado);

            AplicarTornilleria(input, resultado, configuracionBase);
            AplicarRigidizador(input, resultado);
            AplicarStarterRing(input, resultado);

            ValidarResultado(resultado);

            return resultado;
        }

        private ResultadoCalculoModel CrearResultadoBase(CalculoTanqueInputModel input, string normativaAplicada)
        {
            return new ResultadoCalculoModel
            {
                EsValido = true,
                Mensaje = $"Cálculo generado correctamente con normativa {normativaAplicada}.",

                Normativa = normativaAplicada,
                Fabricante = input.Fabricante,
                MaterialPrincipal = input.MaterialPrincipal,

                Diametro = input.Diametro,
                AlturaTotal = input.AlturaTotal,
                AlturaPanelBase = input.AlturaPanelBase,

                ChapasPorAnillo = input.ChapasPorAnillo,
                NumeroAnillos = input.NumeroAnillos,
                AnilloArranque = input.AnilloArranque,
                BordeLibre = input.BordeLibre,
                DensidadLiquido = input.DensidadLiquido,

                PresionHidrostaticaBase = CalcularPresionHidrostaticaBase(input)
            };
        }

        private string ValidarInputBasico(CalculoTanqueInputModel input)
        {
            if (input == null)
                return "No hay datos de entrada para el cálculo.";

            if (string.IsNullOrWhiteSpace(input.Normativa))
                return "La normativa es obligatoria.";

            if (input.Diametro <= 0)
                return "El diámetro debe ser mayor que 0.";

            if (input.AlturaTotal <= 0)
                return "La altura total debe ser mayor que 0.";

            if (input.AlturaPanelBase <= 0)
                return "La altura del panel base debe ser mayor que 0.";

            if (input.NumeroAnillos <= 0)
                return "El número de anillos debe ser mayor que 0.";

            if (input.ChapasPorAnillo <= 0)
                return "El número de chapas por anillo debe ser mayor que 0.";

            if (input.DensidadLiquido <= 0)
                return "La densidad del líquido debe ser mayor que 0.";

            return string.Empty;
        }

        private PosibleConfiguracionModel ObtenerConfiguracion(CalculoTanqueInputModel input)
        {
            var calculoConfiguracionService = new CalculoConfiguracionService();
            return calculoConfiguracionService.ObtenerConfiguracionValida(input);
        }

        private void AplicarConfiguracion(ResultadoCalculoModel resultado, PosibleConfiguracionModel configuracion)
        {
            if (resultado == null)
                return;

            if (configuracion != null)
            {
                resultado.TieneConfiguracion = true;

                resultado.NombreConfiguracion = !string.IsNullOrWhiteSpace(configuracion.Nombre)
                    ? configuracion.Nombre
                    : "Configuración encontrada";

                resultado.NumeroTornillosVerticales = configuracion.NumeroTornillosUnionVertical;
                resultado.NumeroTornillosHorizontales = configuracion.NumeroTornillosUnionHorizontal;
                resultado.NumeroTornillosHorizontalesCalculo = configuracion.NumeroTornillosUnionHorizontalCalculo;
                resultado.DiametroAgujero = configuracion.DiametroAgujero;
            }
            else
            {
                resultado.TieneConfiguracion = false;
                resultado.NombreConfiguracion = "No se encontró configuración válida";
                resultado.NumeroTornillosVerticales = 0;
                resultado.NumeroTornillosHorizontales = 0;
                resultado.NumeroTornillosHorizontalesCalculo = 0;
                resultado.DiametroAgujero = 0;
            }
        }

        private void CalcularAnillos(CalculoTanqueInputModel input, ResultadoCalculoModel resultado, string normativaAplicada)
        {
            if (resultado == null)
                return;

            var calculoEspesoresService = new CalculoEspesoresService();
            resultado.Anillos = calculoEspesoresService.CalcularAnillos(input, normativaAplicada)
                               ?? new List<ResultadoAnilloModel>();
        }

        private void AplicarSeleccionRealDesdeAnillos(ResultadoCalculoModel resultado)
        {
            if (resultado == null || resultado.Anillos == null || resultado.Anillos.Count == 0)
                return;

            var anilloValido = resultado.Anillos.FirstOrDefault(a => a != null && a.EsValido)
                              ?? resultado.Anillos.FirstOrDefault(a => a != null);

            if (anilloValido == null)
                return;

            resultado.NombreConfiguracionCalculada = anilloValido.ConfiguracionAplicada ?? string.Empty;
            resultado.NombreTornilloCalculado = anilloValido.TornilloAplicado ?? string.Empty;
            resultado.DiametroTornilloCalculado = anilloValido.DiametroTornilloAplicado;
            resultado.DiametroAgujeroCalculado = anilloValido.DiametroAgujero;
            resultado.TieneSeleccionRealCalculada =
                !string.IsNullOrWhiteSpace(resultado.NombreConfiguracionCalculada) ||
                !string.IsNullOrWhiteSpace(resultado.NombreTornilloCalculado);

            if (!string.IsNullOrWhiteSpace(anilloValido.ConfiguracionAplicada))
            {
                resultado.TieneConfiguracion = true;
                resultado.NombreConfiguracion = anilloValido.ConfiguracionAplicada;
            }

            if (!string.IsNullOrWhiteSpace(anilloValido.TornilloAplicado))
            {
                resultado.TieneTornilloBase = true;
                resultado.NombreTornilloBase = anilloValido.TornilloAplicado;
                resultado.DiametroTornilloBase = anilloValido.DiametroTornilloAplicado;
            }

            if (anilloValido.DiametroAgujero > 0)
            {
                resultado.DiametroAgujero = anilloValido.DiametroAgujero;
            }
        }

        private void AplicarTornilleria(
            CalculoTanqueInputModel input,
            ResultadoCalculoModel resultado,
            PosibleConfiguracionModel configuracion)
        {
            if (resultado == null)
                return;

            if (resultado.TieneSeleccionRealCalculada)
                return;

            var calculoTornilleriaService = new CalculoTornilleriaService();
            PosibleTornilloModel tornilloBase = calculoTornilleriaService.ObtenerTornilloBase(input, configuracion);

            if (tornilloBase != null)
            {
                resultado.TieneTornilloBase = true;
                resultado.NombreTornilloBase = tornilloBase.CalidadTornillo;
                resultado.DiametroTornilloBase = tornilloBase.Diametro;
            }
            else
            {
                resultado.TieneTornilloBase = false;
                resultado.NombreTornilloBase = "No encontrado";
                resultado.DiametroTornilloBase = 0;
            }
        }

        private void AplicarRigidizador(CalculoTanqueInputModel input, ResultadoCalculoModel resultado)
        {
            if (resultado == null)
                return;

            var calculoRigidizadoresService = new CalculoRigidizadoresService();
            PosibleRigidizadorModel rigidizadorBase = calculoRigidizadoresService.ObtenerRigidizadorBase(input, resultado);

            if (rigidizadorBase != null)
            {
                resultado.TieneRigidizadorBase = true;
                resultado.NombreRigidizadorBase = rigidizadorBase.Tipo;
                resultado.AlturaRigidizadorBase = rigidizadorBase.Altura;
                resultado.EspesorRigidizadorBase = rigidizadorBase.Espesor;
                resultado.PesoRigidizadorBase = rigidizadorBase.Peso;
                resultado.PrecioRigidizadorBase = rigidizadorBase.Precio;
            }
            else
            {
                resultado.TieneRigidizadorBase = false;
                resultado.NombreRigidizadorBase = "No encontrado";
                resultado.AlturaRigidizadorBase = 0;
                resultado.EspesorRigidizadorBase = 0;
                resultado.PesoRigidizadorBase = 0;
                resultado.PrecioRigidizadorBase = 0;
            }
        }

        private void AplicarStarterRing(CalculoTanqueInputModel input, ResultadoCalculoModel resultado)
        {
            if (resultado == null)
                return;

            var calculoStarterRingService = new CalculoStarterRingService();
            PosibleStarterRingModel starterRingBase = calculoStarterRingService.ObtenerStarterRingBase(input);

            if (starterRingBase != null)
            {
                resultado.TieneStarterRing = true;
                resultado.AlturaStarterRing = starterRingBase.Altura;
                resultado.DistanciaFStarterRing = starterRingBase.DistanciaF;
                resultado.ShearKeysPorLineaStarterRing = starterRingBase.ShearKeysPerLine;
                resultado.FStarterRingTexto = starterRingBase.F != null
                    ? string.Join(", ", starterRingBase.F)
                    : string.Empty;
                resultado.MaxShearKeysPorPlanchaTexto = starterRingBase.MaxShearKeysPerSheet != null
                    ? string.Join(", ", starterRingBase.MaxShearKeysPerSheet)
                    : string.Empty;
            }
            else
            {
                resultado.TieneStarterRing = false;
                resultado.AlturaStarterRing = 0;
                resultado.DistanciaFStarterRing = 0;
                resultado.ShearKeysPorLineaStarterRing = 0;
                resultado.FStarterRingTexto = string.Empty;
                resultado.MaxShearKeysPorPlanchaTexto = string.Empty;
            }
        }

        private void ValidarResultado(ResultadoCalculoModel resultado)
        {
            if (resultado == null)
                return;

            if (resultado.Anillos == null || resultado.Anillos.Count == 0)
            {
                resultado.EsValido = false;
                resultado.Mensaje = "No se han generado anillos de cálculo.";
                return;
            }

            if (resultado.Anillos.Any(a => a == null))
            {
                resultado.EsValido = false;
                resultado.Mensaje = "Se han detectado anillos nulos en el cálculo.";
                return;
            }

            if (resultado.Anillos.Any(a => !a.EsValido))
            {
                resultado.EsValido = false;
                resultado.Mensaje = "Hay anillos sin espesor válido.";
                return;
            }

            resultado.EsValido = true;
            resultado.Mensaje = $"Cálculo generado correctamente con normativa {resultado.Normativa}.";
        }

        private double CalcularPresionHidrostaticaBase(CalculoTanqueInputModel input)
        {
            if (input == null)
                return 0;

            if (input.DensidadLiquido <= 0 || input.AlturaTotal <= 0)
                return 0;

            var formulaPresionService = new FormulaPresionService();
            return formulaPresionService.CalcularPresionHidrostaticaBase(
                input.DensidadLiquido,
                input.AlturaTotal);
        }
    }
}