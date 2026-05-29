using Microsoft.JSInterop;

namespace TankDesigner.Web.Services
{
    /// <summary>
    /// Compatibilidad con versiones anteriores.
    /// Antes algunas páginas usaban BrowserProyectoSnapshotService.
    /// Ahora la clase correcta es BrowserProyectoActivoService, pero se mantiene esta clase
    /// para que no falle la compilación si queda alguna referencia antigua.
    /// </summary>
    public class BrowserProyectoSnapshotService : BrowserProyectoActivoService
    {
        public BrowserProyectoSnapshotService(IJSRuntime js) : base(js)
        {
        }
    }
}
