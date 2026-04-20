using Newtonsoft.Json;
using TankDesigner.Core.Models.Catalogos;
using TankDesigner.Core.Models.Presupuestos;

namespace TankDesigner.Core.Services
{
    public class JsonCatalogService
    {
        private string ObtenerRutaFabricante(string fabricante)
        {
            string fabricanteNormalizado = NormalizarFabricante(fabricante);

            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Plantillas",
                "TankStructuralDesignerFiles",
                "RutaJson",
                fabricanteNormalizado
            );
        }

        public List<PosibleRigidizadorModel> ObtenerRigidizadoresSuperiores(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesRigidizadoresSuperiores.json");
            return CargarListaDesdeJson<PosibleRigidizadorModel>(ruta);
        }
    
        private static string NormalizarFabricante(string fabricante)
        {
            if (string.IsNullOrWhiteSpace(fabricante))
                return "BALMORAL";

            string clave = fabricante.Trim().ToUpperInvariant();

            if (clave.Contains("PERMASTORE"))
                return "PERMASTORE";

            if (clave.Contains("DL2"))
                return "DL2";

            return "BALMORAL";
        }

        private List<T> CargarListaDesdeJson<T>(string rutaArchivo)
        {
            try
            {
                if (!File.Exists(rutaArchivo))
                    return new List<T>();

                string json = File.ReadAllText(rutaArchivo);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<T>();

                List<T>? resultado = JsonConvert.DeserializeObject<List<T>>(json);
                return resultado ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        private T CargarObjetoDesdeJson<T>(string rutaArchivo) where T : new()
        {
            try
            {
                if (!File.Exists(rutaArchivo))
                    return new T();

                string json = File.ReadAllText(rutaArchivo);

                if (string.IsNullOrWhiteSpace(json))
                    return new T();

                T? resultado = JsonConvert.DeserializeObject<T>(json);
                return resultado ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        private string ObtenerRutaConFallback(string fabricante, string nombreArchivo)
        {
            string rutaPrincipal = Path.Combine(ObtenerRutaFabricante(fabricante), nombreArchivo);
            if (File.Exists(rutaPrincipal))
                return rutaPrincipal;

            string rutaBalmoral = Path.Combine(ObtenerRutaFabricante("BALMORAL"), nombreArchivo);
            if (File.Exists(rutaBalmoral))
                return rutaBalmoral;

            return rutaPrincipal;
        }

        public List<PosiblePlanchaModel> CargarPlanchas(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesPlanchas.json");
            return CargarListaDesdeJson<PosiblePlanchaModel>(ruta);
        }

        public List<PosibleConfiguracionModel> CargarConfiguraciones(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesConfiguraciones.json");
            return CargarListaDesdeJson<PosibleConfiguracionModel>(ruta);
        }

        public List<PosibleRigidizadorModel> CargarRigidizadores(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesRigidizadores.json");
            return CargarListaDesdeJson<PosibleRigidizadorModel>(ruta);
        }

        public List<PosibleStarterRingModel> CargarStarterRings(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesSR.json");
            return CargarListaDesdeJson<PosibleStarterRingModel>(ruta);
        }

        public List<PosibleTornilloModel> CargarTornillos(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "ListaPosiblesTornillos.json");
            return CargarListaDesdeJson<PosibleTornilloModel>(ruta);
        }

        public PresupuestoConfigJsonModel CargarDatosInstalacion(string fabricante)
        {
            string ruta = ObtenerRutaConFallback(fabricante, "Instalacion_Data.json");
            return CargarObjetoDesdeJson<PresupuestoConfigJsonModel>(ruta);
        }
    }
}