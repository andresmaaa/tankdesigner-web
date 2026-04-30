using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services.Ai;

// Servicio que conecta la aplicación con Gemini.
// La IA no calcula el tanque: interpreta datos ya calculados por el motor real.
public class AiEngineeringService
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly AiPreanalisisTecnicoService _preanalisis;

    public AiEngineeringService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        AiPreanalisisTecnicoService preanalisis)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _preanalisis = preanalisis;
    }

    // ANÁLISIS AUTOMÁTICO DEL PROYECTO
    public async Task<AiAnalisisResultadoDto> AnalizarProyectoAsync(
        ProyectoGeneralModel proyecto,
        TankModel tanque,
        CargasModel cargas,
        InstalacionModel instalacion,
        ResultadoCalculoModel? resultado)
    {
        var dtoTecnico = _preanalisis.CrearDtoTecnico(
            proyecto,
            tanque,
            cargas,
            instalacion,
            resultado);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return CrearRespuestaFallback(dtoTecnico, "No se ha configurado Gemini:ApiKey.");

        var dtoTecnicoJson = JsonSerializer.Serialize(dtoTecnico, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var promptSistema = """
Eres un asistente técnico especializado en diseño estructural de tanques atornillados de acero.

Reglas obligatorias:
- Responde siempre en español.
- No inventes datos.
- No recalcules la normativa completa.
- No sustituyas al cálculo estructural del software.
- Solo puedes usar los valores recibidos en el JSON técnico.
- Si falta información, indícalo claramente.
- Tus recomendaciones deben estar basadas en datos reales del proyecto.
- Debes detectar problemas de seguridad, coherencia técnica, cargas, materiales, presupuesto y optimización.
- No des respuestas genéricas.
""";

        var promptUsuario = """
El software ya ha calculado el tanque. Analiza únicamente el siguiente DTO técnico:

"""
        + dtoTecnicoJson
        + """

Devuelve SOLO un JSON válido con esta estructura:

{
  "resumenGeneral": "Resumen técnico breve del proyecto.",
  "nivelRiesgo": "bajo | medio | alto",
  "hallazgos": [
    {
      "tipo": "error | advertencia | sugerencia | info",
      "campo": "campo afectado",
      "titulo": "título breve",
      "descripcion": "explicación técnica basada en datos reales",
      "recomendacion": "acción concreta recomendada",
      "prioridad": 1
    }
  ]
}

Prioridades:
1 = informativo
2 = mejora menor
3 = mejora recomendable
4 = advertencia importante
5 = problema crítico

No incluyas Markdown.
No expliques nada fuera del JSON.
""";

        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                resumenGeneral = new { type = "string" },
                nivelRiesgo = new
                {
                    type = "string",
                    @enum = new[] { "bajo", "medio", "alto" }
                },
                hallazgos = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            tipo = new
                            {
                                type = "string",
                                @enum = new[] { "error", "advertencia", "sugerencia", "info" }
                            },
                            campo = new { type = "string" },
                            titulo = new { type = "string" },
                            descripcion = new { type = "string" },
                            recomendacion = new { type = "string" },
                            prioridad = new
                            {
                                type = "integer",
                                minimum = 1,
                                maximum = 5
                            }
                        },
                        required = new[]
                        {
                            "tipo",
                            "campo",
                            "titulo",
                            "descripcion",
                            "recomendacion",
                            "prioridad"
                        }
                    }
                }
            },
            required = new[] { "resumenGeneral", "nivelRiesgo", "hallazgos" }
        };

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new object[]
                {
                    new { text = promptSistema }
                }
            },
            contents = new object[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = promptUsuario }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseJsonSchema = schema,
                temperature = 0.15
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return CrearRespuestaFallback(
                    dtoTecnico,
                    $"Error Gemini ({(int)response.StatusCode}): {LimpiarErrorGemini(responseText)}");

            using var document = JsonDocument.Parse(responseText);

            var jsonSalida = ExtraerTextoRespuesta(document.RootElement);

            if (string.IsNullOrWhiteSpace(jsonSalida))
                return CrearRespuestaFallback(dtoTecnico, "Gemini no devolvió contenido estructurado.");

            var resultadoDto = JsonSerializer.Deserialize<AiAnalisisResultadoDto>(
                jsonSalida,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (resultadoDto is null)
                return CrearRespuestaFallback(dtoTecnico, "No se pudo deserializar la respuesta de Gemini.");

            FusionarHallazgosPrevios(resultadoDto, dtoTecnico);

            return resultadoDto;
        }
        catch (Exception ex)
        {
            return CrearRespuestaFallback(dtoTecnico, ex.Message);
        }
    }

    // CHAT SOBRE EL PROYECTO
    public async Task<string> PreguntarSobreProyectoAsync(
        ProyectoGeneralModel proyecto,
        TankModel tanque,
        CargasModel cargas,
        InstalacionModel instalacion,
        ResultadoCalculoModel? resultado,
        string preguntaUsuario)
    {
        if (string.IsNullOrWhiteSpace(preguntaUsuario))
            throw new InvalidOperationException("La pregunta no puede estar vacía.");

        var dtoTecnico = _preanalisis.CrearDtoTecnico(
            proyecto,
            tanque,
            cargas,
            instalacion,
            resultado);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return "No se ha configurado Gemini:ApiKey. Aun así, el preanálisis interno ha detectado estos puntos: "
                   + string.Join(" | ", dtoTecnico.HallazgosPrevios.Select(h => $"{h.Titulo}: {h.Descripcion}"));

        var dtoTecnicoJson = JsonSerializer.Serialize(dtoTecnico, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var promptSistema = """
Eres un asistente técnico especializado en diseño estructural de tanques atornillados de acero.

Reglas:
- Responde siempre en español.
- No inventes datos.
- No recalcules normativa.
- No sustituyas el cálculo estructural del software.
- Solo interpreta datos reales recibidos.
- Si falta información, dilo claramente.
- Sé técnico, directo y útil.
- Puedes proponer mejoras de seguridad, presupuesto, materiales, cargas o informe, pero siempre justificadas con los datos disponibles.
""";

        var promptUsuario = $"""
DTO técnico real del proyecto:
{dtoTecnicoJson}

Pregunta del usuario:
{preguntaUsuario}

Responde de forma clara, técnica y centrada solo en este proyecto.
""";

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new object[]
                {
                    new { text = promptSistema }
                }
            },
            contents = new object[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = promptUsuario }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.15
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Error Gemini ({(int)response.StatusCode}): {LimpiarErrorGemini(responseText)}");

        using var document = JsonDocument.Parse(responseText);

        var texto = ExtraerTextoRespuesta(document.RootElement);

        if (string.IsNullOrWhiteSpace(texto))
            throw new InvalidOperationException("Gemini no devolvió respuesta.");

        return texto;
    }

    private static void FusionarHallazgosPrevios(
      AiAnalisisResultadoDto resultadoDto,
      AiProyectoTecnicoDto dtoTecnico)
    {
        if (dtoTecnico.HallazgosPrevios.Count == 0)
            return;

        resultadoDto.Hallazgos ??= new List<AiHallazgoDto>();

        foreach (var hallazgo in dtoTecnico.HallazgosPrevios)
            resultadoDto.Hallazgos.Insert(0, hallazgo);
    }

    private static AiAnalisisResultadoDto CrearRespuestaFallback(
        AiProyectoTecnicoDto dtoTecnico,
        string motivo)
    {
        var hallazgos = dtoTecnico.HallazgosPrevios.ToList();

        hallazgos.Insert(0, new AiHallazgoDto
        {
            Tipo = "advertencia",
            Campo = "ia",
            Titulo = "Análisis IA no disponible",
            Descripcion = motivo,
            Recomendacion = "Revisa la configuración de Gemini. Mientras tanto, se muestran los hallazgos generados internamente por el preanálisis técnico.",
            Prioridad = 4
        });

        var nivelRiesgo = hallazgos.Any(h => h.Prioridad >= 5)
            ? "alto"
            : hallazgos.Any(h => h.Prioridad >= 4)
                ? "medio"
                : "bajo";

        return new AiAnalisisResultadoDto
        {
            ResumenGeneral = "Se ha generado un análisis técnico interno basado en los resultados reales disponibles del proyecto. La respuesta avanzada de Gemini no está disponible o no ha podido procesarse correctamente.",
            NivelRiesgo = nivelRiesgo,
            Hallazgos = hallazgos
        };
    }

    private static string ExtraerTextoRespuesta(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var candidate = candidates[0];

        if (!candidate.TryGetProperty("content", out var content))
            return string.Empty;

        if (!content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textNode))
                return textNode.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string LimpiarErrorGemini(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);

            if (document.RootElement.TryGetProperty("error", out var error))
            {
                var mensaje = error.TryGetProperty("message", out var messageNode)
                    ? messageNode.GetString()
                    : null;

                var estado = error.TryGetProperty("status", out var statusNode)
                    ? statusNode.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(mensaje) && !string.IsNullOrWhiteSpace(estado))
                    return $"{estado}: {mensaje}";

                if (!string.IsNullOrWhiteSpace(mensaje))
                    return mensaje;
            }
        }
        catch
        {
            // Si falla el parseo, se devuelve el texto original.
        }

        return responseText;
    }
}