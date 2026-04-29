using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services.Ai;

public class AiPreanalisisTecnicoService
{
    public AiProyectoTecnicoDto CrearDtoTecnico(
        ProyectoGeneralModel proyecto,
        TankModel tanque,
        CargasModel cargas,
        InstalacionModel instalacion,
        ResultadoCalculoModel? resultado)
    {
        var dto = new AiProyectoTecnicoDto
        {
            NombreProyecto = proyecto.NombreProyecto ?? string.Empty,
            Normativa = resultado?.Normativa ?? cargas.NormativaAplicada ?? string.Empty,
            Fabricante = resultado?.Fabricante ?? string.Empty,

            Diametro = resultado?.Diametro > 0 ? resultado.Diametro : tanque.Diametro,
            AlturaTotal = resultado?.AlturaTotal > 0 ? resultado.AlturaTotal : tanque.AlturaTotal,
            AlturaPanelBase = resultado?.AlturaPanelBase > 0 ? resultado.AlturaPanelBase : tanque.AlturaPanelBase,
            NumeroAnillos = resultado?.NumeroAnillos > 0 ? resultado.NumeroAnillos : tanque.NumeroAnillos,
            ChapasPorAnillo = resultado?.ChapasPorAnillo > 0 ? resultado.ChapasPorAnillo : tanque.ChapasPorAnillo,

            MaterialPrincipal = resultado?.MaterialPrincipal ?? string.Empty,
            ConfiguracionCalculada = resultado?.NombreConfiguracionCalculada ?? resultado?.NombreConfiguracion ?? string.Empty,
            TornilloCalculado = resultado?.NombreTornilloCalculado ?? resultado?.NombreTornilloBase ?? string.Empty,

            TieneRigidizador = resultado?.TieneRigidizadorBase ?? false,
            Rigidizador = resultado?.NombreRigidizadorBase ?? string.Empty,

            TieneStarterRing = resultado?.TieneStarterRing ?? false,

            VelocidadViento = cargas.VelocidadViento,
            ClaseExposicion = cargas.ClaseExposicion ?? string.Empty,

            Ss = cargas.Ss,
            S1 = cargas.S1,
            SiteClass = cargas.SiteClass ?? string.Empty,

            TipoTecho = cargas.RoofType ?? string.Empty,
            CargaMuertaTecho = cargas.RoofDeadLoad,
            CargaNieveTecho = cargas.RoofSnowLoad,
            CargaVivaTecho = cargas.RoofLiveLoad,

            CalculoValido = resultado?.EsValido ?? false,
            MensajeCalculo = resultado?.Mensaje ?? string.Empty
        };

        if (resultado?.Anillos != null)
        {
            dto.Anillos = resultado.Anillos
                .OrderBy(a => a.NumeroAnillo)
                .Select(CrearAnilloTecnico)
                .ToList();
        }

        dto.HallazgosPrevios = CrearHallazgosPrevios(dto, resultado);

        return dto;
    }

    private static AiAnilloTecnicoDto CrearAnilloTecnico(ResultadoAnilloModel anillo)
    {
        return new AiAnilloTecnicoDto
        {
            NumeroAnillo = anillo.NumeroAnillo,

            AlturaInferior = anillo.AlturaInferior,
            AlturaSuperior = anillo.AlturaSuperior,

            Material = anillo.MaterialAplicado ?? string.Empty,
            Configuracion = anillo.ConfiguracionAplicada ?? string.Empty,
            Tornillo = anillo.TornilloAplicado ?? string.Empty,

            Presion = anillo.Presion,
            EspesorRequerido = anillo.EspesorRequerido,
            EspesorSeleccionado = anillo.EspesorSeleccionado,

            AprovechamientoEspesor = Ratio(anillo.EspesorRequerido, anillo.EspesorSeleccionado),

            TensionNeta = anillo.NetTensileStress,
            TensionAdmisible = anillo.AllowableTensileStress,
            AprovechamientoTraccion = Ratio(anillo.NetTensileStress, anillo.AllowableTensileStress),

            CortanteTornillo = anillo.BoltShearStress,
            CortanteAdmisible = anillo.AllowableShearStress,
            AprovechamientoCortante = Ratio(anillo.BoltShearStress, anillo.AllowableShearStress),

            Aplastamiento = anillo.HoleBearingStress,
            AplastamientoAdmisible = anillo.AllowableBearingStress,
            AprovechamientoAplastamiento = Ratio(anillo.HoleBearingStress, anillo.AllowableBearingStress),

            CumpleTraccion = anillo.CumpleTraccion,
            CumpleCortante = anillo.CumpleCortante,
            CumpleAplastamiento = anillo.CumpleAplastamiento,

            CumpleAxial = anillo.AxialEsValido,
            CumpleViento = anillo.WindEsValido,
            CumpleSismo = anillo.SeismicEsValido,
            CumpleCombinado = anillo.CombinedEsValido,

            EstadoResumen = anillo.EstadoResumen ?? string.Empty,
            TipoFallo = anillo.TipoFallo ?? string.Empty
        };
    }

    private static List<AiHallazgoDto> CrearHallazgosPrevios(
        AiProyectoTecnicoDto dto,
        ResultadoCalculoModel? resultado)
    {
        var hallazgos = new List<AiHallazgoDto>();

        if (resultado == null)
        {
            hallazgos.Add(new AiHallazgoDto
            {
                Tipo = "error",
                Campo = "resultado",
                Titulo = "No hay resultado de cálculo",
                Descripcion = "La IA no puede analizar técnicamente el tanque porque no existe un resultado calculado.",
                Recomendacion = "Ejecuta el cálculo estructural antes de solicitar el análisis IA.",
                Prioridad = 5
            });

            return hallazgos;
        }

        if (!resultado.EsValido)
        {
            hallazgos.Add(new AiHallazgoDto
            {
                Tipo = "error",
                Campo = "calculo",
                Titulo = "El cálculo no es válido",
                Descripcion = $"El motor de cálculo indica que el proyecto no es válido. Mensaje: {resultado.Mensaje}",
                Recomendacion = "Revisa los datos de geometría, cargas, configuración, materiales y catálogos antes de optimizar.",
                Prioridad = 5
            });
        }

        if (dto.Anillos.Count == 0)
        {
            hallazgos.Add(new AiHallazgoDto
            {
                Tipo = "error",
                Campo = "anillos",
                Titulo = "No hay anillos calculados",
                Descripcion = "No se han encontrado anillos en el resultado del cálculo.",
                Recomendacion = "Comprueba que el cálculo genera la lista de anillos correctamente.",
                Prioridad = 5
            });
        }

        foreach (var anillo in dto.Anillos)
        {
            if (!anillo.CumpleTraccion || !anillo.CumpleCortante || !anillo.CumpleAplastamiento)
            {
                hallazgos.Add(new AiHallazgoDto
                {
                    Tipo = "error",
                    Campo = $"anillo {anillo.NumeroAnillo}",
                    Titulo = "Comprobación resistente no válida",
                    Descripcion = $"El anillo {anillo.NumeroAnillo} no cumple alguna comprobación principal: tracción, cortante o aplastamiento.",
                    Recomendacion = "Revisar espesor, material, tornillería o configuración aplicada en este anillo.",
                    Prioridad = 5
                });
            }

            if (anillo.AprovechamientoTraccion > 0.9)
            {
                hallazgos.Add(new AiHallazgoDto
                {
                    Tipo = "advertencia",
                    Campo = $"anillo {anillo.NumeroAnillo}",
                    Titulo = "Anillo muy aprovechado a tracción",
                    Descripcion = $"El anillo {anillo.NumeroAnillo} tiene un aprovechamiento de tracción cercano al límite.",
                    Recomendacion = "Valorar si conviene aumentar espesor, mejorar material o cambiar configuración si el margen es insuficiente.",
                    Prioridad = 4
                });
            }

            if (anillo.AprovechamientoEspesor > 0 && anillo.AprovechamientoEspesor < 0.55)
            {
                hallazgos.Add(new AiHallazgoDto
                {
                    Tipo = "sugerencia",
                    Campo = $"anillo {anillo.NumeroAnillo}",
                    Titulo = "Posible sobredimensionamiento de espesor",
                    Descripcion = $"El espesor requerido está bastante por debajo del espesor seleccionado en el anillo {anillo.NumeroAnillo}.",
                    Recomendacion = "Revisar si existe una chapa comercial inferior válida en catálogo antes de cerrar presupuesto.",
                    Prioridad = 2
                });
            }
        }

        if (dto.VelocidadViento <= 0)
        {
            hallazgos.Add(new AiHallazgoDto
            {
                Tipo = "advertencia",
                Campo = "viento",
                Titulo = "Velocidad de viento no definida",
                Descripcion = "La velocidad de viento es cero o no está informada.",
                Recomendacion = "Introduce la velocidad de viento real del emplazamiento antes de validar el diseño.",
                Prioridad = 4
            });
        }

        if (string.Equals(dto.TipoTecho, "None", StringComparison.OrdinalIgnoreCase))
        {
            if (dto.CargaMuertaTecho != 0 || dto.CargaNieveTecho != 0 || dto.CargaVivaTecho != 0)
            {
                hallazgos.Add(new AiHallazgoDto
                {
                    Tipo = "error",
                    Campo = "techo",
                    Titulo = "Cargas de techo incoherentes",
                    Descripcion = "El tipo de techo está en None, pero existen cargas de techo distintas de cero.",
                    Recomendacion = "Si no hay techo, las cargas de techo deben quedar a cero en UI, cálculo e informe.",
                    Prioridad = 5
                });
            }
        }

        return hallazgos;
    }

    private static double Ratio(double valor, double limite)
    {
        if (limite <= 0)
            return 0;

        return Math.Round(valor / limite, 3);
    }
}