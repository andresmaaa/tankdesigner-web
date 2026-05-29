using Microsoft.JSInterop;

namespace TankDesigner.Web.Services
{
    /// <summary>
    /// Guarda en el navegador el identificador del proyecto que el usuario tiene abierto.
    /// Esto permite recuperar el proyecto activo tras F5, un reinicio del circuito de Blazor
    /// o un redeploy de Railway, siempre que el proyecto ya esté guardado en base de datos.
    /// </summary>
    public class BrowserProyectoActivoService
    {
        private const string ProyectoActivoKey = "TankDesigner.ProyectoActivoId";
        private readonly IJSRuntime _js;

        public BrowserProyectoActivoService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task GuardarProyectoActivoAsync(int proyectoId)
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", ProyectoActivoKey, proyectoId.ToString());
            }
            catch
            {
                // Si el navegador bloquea localStorage, la aplicación debe seguir funcionando.
            }
        }

        public async Task<int?> ObtenerProyectoActivoAsync()
        {
            try
            {
                var valor = await _js.InvokeAsync<string?>("localStorage.getItem", ProyectoActivoKey);

                if (int.TryParse(valor, out var id) && id > 0)
                    return id;
            }
            catch
            {
                // En prerender, desconexiones o navegadores restrictivos, devolvemos null.
            }

            return null;
        }

        public async Task LimpiarProyectoActivoAsync()
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", ProyectoActivoKey);
            }
            catch
            {
                // No bloquea la navegación si no se puede limpiar.
            }
        }
    }
}
