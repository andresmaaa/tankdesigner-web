using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services.Ai;

// Servicio que conecta tu aplicación con Gemini (IA)
// Se usa para analizar proyectos y responder preguntas técnicas
public class AiEngineeringService
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;

    public AiEngineeringService(HttpClient httpClient, IOptions<AiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    // ANALISIS AUTOMÁTICO DEL PROYECTO
    public async Task<AiAnalisisResultadoDto> AnalizarProyectoAsync(
        ProyectoGeneralModel proyecto,
        TankModel tanque,
        CargasModel cargas,
        InstalacionModel instalacion,
        ResultadoCalculoModel? resultado)
    {
        // Verifica que la API Key esté configurada
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("No se ha configurado Gemini:ApiKey.");

        // Se agrupan todos los datos del proyecto
        var datosProyecto = new
        {
            Proyecto = proyecto,
            Tanque = tanque,
            Cargas = cargas,
            Instalacion = instalacion,
            Resultado = resultado
        };

        // Se serializa a JSON para enviarlo a la IA
        var datosProyectoJson = JsonSerializer.Serialize(datosProyecto, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Prompt de sistema: reglas de comportamiento de la IA
        var promptSistema = """
Eres un asistente técnico de ingeniería especializado en diseño de tanques industriales.

Reglas:
- No inventes datos.
- No recalcules normativa.
- No sustituyas normativa ni validación de ingeniería.
- Solo analiza coherencia, completitud, consistencia e interpretación técnica.
- Si faltan datos, dilo claramente.
- Sé concreto y útil.
- Responde siempre en español.
""";

        // Prompt de usuario: qué queremos que devuelva
        var promptUsuario = $"""
Analiza este proyecto técnico y devuelve:
- resumenGeneral
- nivelRiesgo (bajo, medio o alto)
- hallazgos

Cada hallazgo debe indicar:
- tipo (error, advertencia, sugerencia o info)
- campo
- titulo
- descripcion
- recomendacion
- prioridad (1 a 5)

Datos del proyecto:
{datosProyectoJson}
""";

        // Schema JSON que obligamos a cumplir a la IA
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

        // Cuerpo de la petición a Gemini
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
                temperature = 0.2 // baja creatividad, más técnico
            }
        };

        // URL del modelo Gemini
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);

        // API Key en cabecera
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Se envía la petición
        using var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        // Si falla la llamada → error detallado
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Error Gemini ({(int)response.StatusCode}): {LimpiarErrorGemini(responseText)}");

        // Se parsea la respuesta
        using var document = JsonDocument.Parse(responseText);

        // Se extrae el JSON generado por la IA
        var jsonSalida = ExtraerTextoRespuesta(document.RootElement);

        if (string.IsNullOrWhiteSpace(jsonSalida))
            throw new InvalidOperationException("Gemini no devolvió contenido estructurado.");

        // Se convierte a DTO
        var resultadoDto = JsonSerializer.Deserialize<AiAnalisisResultadoDto>(
            jsonSalida,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (resultadoDto is null)
            throw new InvalidOperationException("No se pudo deserializar la respuesta de Gemini.");

        return resultadoDto;
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
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("No se ha configurado Gemini:ApiKey.");

        if (string.IsNullOrWhiteSpace(preguntaUsuario))
            throw new InvalidOperationException("La pregunta no puede estar vacía.");

        // Datos del proyecto
        var datosProyecto = new
        {
            Proyecto = proyecto,
            Tanque = tanque,
            Cargas = cargas,
            Instalacion = instalacion,
            Resultado = resultado
        };

        var datosProyectoJson = JsonSerializer.Serialize(datosProyecto, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Prompt de sistema (más enfocado a responder preguntas)
        var promptSistema = """
Eres un asistente técnico de ingeniería especializado en diseño de tanques industriales.

Reglas:
- No inventes datos.
- No recalcules normativa.
- No sustituyas el cálculo estructural del software.
- Solo interpreta y explica lo que se ve en los datos del proyecto.
- Si falta información, dilo claramente.
- Responde siempre en español.
- Sé técnico, claro y útil.
""";

        var promptUsuario = $"""
Proyecto actual en JSON:
{datosProyectoJson}

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
                temperature = 0.2
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

    // Extrae el texto de la respuesta de Gemini
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

    // Limpia errores de Gemini para mostrarlos mejor
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
            // Si falla el parseo, devuelve texto original
        }

        return responseText;
    }
}