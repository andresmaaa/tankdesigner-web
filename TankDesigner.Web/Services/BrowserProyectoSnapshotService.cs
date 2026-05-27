using Microsoft.JSInterop;
using Newtonsoft.Json;
using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services
{
    /// <summary>
    /// Guarda una copia temporal del proyecto en el navegador para que,
    /// si el usuario refresca la página, no se pierda el estado actual.
    ///
    /// Importante: esto NO sustituye al guardado en base de datos.
    /// Solo mantiene la sesión de trabajo abierta tras un F5/recarga.
    /// </summary>
    public class BrowserProyectoSnapshotService
    {
        private readonly IJSRuntime _js;

        private const string KeyPrefix = "tankdesigner.proyecto.enCurso";

        public BrowserProyectoSnapshotService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task GuardarAsync(ProyectoState estado, string? usuarioKey, string? rutaActual)
        {
            try
            {
                var snapshot = ProyectoSnapshotDto.FromState(estado, rutaActual);
                var json = JsonConvert.SerializeObject(snapshot, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                await _js.InvokeVoidAsync("localStorage.setItem", GetKey(usuarioKey), json);
            }
            catch
            {
                // No bloqueamos la aplicación si el navegador no permite localStorage
                // o si el circuito Blazor se está cerrando durante la recarga.
            }
        }

        public async Task<bool> RestaurarAsync(ProyectoState estado, string? usuarioKey)
        {
            try
            {
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", GetKey(usuarioKey));

                if (string.IsNullOrWhiteSpace(json))
                    return false;

                var snapshot = JsonConvert.DeserializeObject<ProyectoSnapshotDto>(json);

                if (snapshot == null || snapshot.FechaSnapshotUtc == default)
                    return false;

                // Evita restaurar copias muy antiguas por accidente.
                if (DateTime.UtcNow - snapshot.FechaSnapshotUtc > TimeSpan.FromDays(7))
                    return false;

                estado.ProyectoIdActual = snapshot.ProyectoIdActual;
                estado.Proyecto = snapshot.Proyecto ?? new ProyectoGeneralModel();
                estado.Tanque = snapshot.Tanque ?? new TankModel();
                estado.Cargas = snapshot.Cargas ?? new CargasModel();
                estado.Instalacion = snapshot.Instalacion ?? new InstalacionModel();
                estado.Instalacion.Emplazamiento ??= new EmplazamientoInstalacionModel();
                estado.Resultado = snapshot.Resultado ?? new ResultadoCalculoModel();

                NormalizarEstado(estado);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task LimpiarAsync(string? usuarioKey)
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", GetKey(usuarioKey));
            }
            catch
            {
            }
        }

        private static string GetKey(string? usuarioKey)
        {
            var usuario = string.IsNullOrWhiteSpace(usuarioKey)
                ? "anonimo"
                : usuarioKey.Trim().ToLowerInvariant();

            foreach (var invalid in Path.GetInvalidFileNameChars())
                usuario = usuario.Replace(invalid, '_');

            usuario = usuario
                .Replace("@", "_")
                .Replace(".", "_")
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_");

            return $"{KeyPrefix}.{usuario}";
        }

        private static void NormalizarEstado(ProyectoState estado)
        {
            estado.Proyecto ??= new ProyectoGeneralModel();
            estado.Tanque ??= new TankModel();
            estado.Cargas ??= new CargasModel();
            estado.Instalacion ??= new InstalacionModel();
            estado.Instalacion.Emplazamiento ??= new EmplazamientoInstalacionModel();
            estado.Resultado ??= new ResultadoCalculoModel();

            if (estado.Tanque.ChapasPorAnillo <= 0)
                estado.Tanque.ChapasPorAnillo = 16;

            if (estado.Tanque.NumeroAnillos <= 0)
                estado.Tanque.NumeroAnillos = estado.Resultado.NumeroAnillos > 0 ? estado.Resultado.NumeroAnillos : 6;

            if (estado.Tanque.AnilloArranque <= 0)
                estado.Tanque.AnilloArranque = 1;

            if (estado.Tanque.AlturaPanelBase <= 0)
                estado.Tanque.AlturaPanelBase = 1200;

            estado.Tanque.AlturasAnillos ??= new List<double>();
            estado.Tanque.MaterialesAnillos ??= new List<string>();
            estado.Tanque.ConfiguracionesAnillos ??= new List<string>();

            string materialDefault = !string.IsNullOrWhiteSpace(estado.Proyecto.MaterialPrincipal)
                ? estado.Proyecto.MaterialPrincipal.Trim()
                : "S235";

            while (estado.Tanque.MaterialesAnillos.Count < estado.Tanque.NumeroAnillos)
                estado.Tanque.MaterialesAnillos.Add(materialDefault);

            while (estado.Tanque.ConfiguracionesAnillos.Count < estado.Tanque.NumeroAnillos)
                estado.Tanque.ConfiguracionesAnillos.Add(string.Empty);

            if (estado.Tanque.MaterialesAnillos.Count > estado.Tanque.NumeroAnillos)
                estado.Tanque.MaterialesAnillos = estado.Tanque.MaterialesAnillos.Take(estado.Tanque.NumeroAnillos).ToList();

            if (estado.Tanque.ConfiguracionesAnillos.Count > estado.Tanque.NumeroAnillos)
                estado.Tanque.ConfiguracionesAnillos = estado.Tanque.ConfiguracionesAnillos.Take(estado.Tanque.NumeroAnillos).ToList();

            for (int i = 0; i < estado.Tanque.NumeroAnillos; i++)
            {
                if (string.IsNullOrWhiteSpace(estado.Tanque.MaterialesAnillos[i]))
                    estado.Tanque.MaterialesAnillos[i] = materialDefault;
            }
        }

        private sealed class ProyectoSnapshotDto
        {
            public DateTime FechaSnapshotUtc { get; set; } = DateTime.UtcNow;
            public string RutaActual { get; set; } = string.Empty;
            public int? ProyectoIdActual { get; set; }
            public ProyectoGeneralModel Proyecto { get; set; } = new();
            public TankModel Tanque { get; set; } = new();
            public CargasModel Cargas { get; set; } = new();
            public InstalacionModel Instalacion { get; set; } = new();
            public ResultadoCalculoModel Resultado { get; set; } = new();

            public static ProyectoSnapshotDto FromState(ProyectoState estado, string? rutaActual)
            {
                estado.Instalacion ??= new InstalacionModel();
                estado.Instalacion.Emplazamiento ??= new EmplazamientoInstalacionModel();

                return new ProyectoSnapshotDto
                {
                    FechaSnapshotUtc = DateTime.UtcNow,
                    RutaActual = rutaActual ?? string.Empty,
                    ProyectoIdActual = estado.ProyectoIdActual,
                    Proyecto = estado.Proyecto ?? new ProyectoGeneralModel(),
                    Tanque = estado.Tanque ?? new TankModel(),
                    Cargas = estado.Cargas ?? new CargasModel(),
                    Instalacion = estado.Instalacion,
                    Resultado = estado.Resultado ?? new ResultadoCalculoModel()
                };
            }
        }
    }
}
