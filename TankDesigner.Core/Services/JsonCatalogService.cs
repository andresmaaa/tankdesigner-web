using Newtonsoft.Json;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Core.Services
{
    // Servicio encargado de cargar datos de catálogo desde archivos JSON.
    // Se usa para obtener planchas, configuraciones, tornillos, etc.
    public class JsonCatalogService
    {
        // Construye la ruta base donde están los JSON del fabricante.
        private string ObtenerRutaFabricante(string fabricante)
        {
            if (string.IsNullOrWhiteSpace(fabricante))
                throw new ArgumentException("El fabricante no puede estar vacío.");

            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Plantillas",
                "TankStructuralDesignerFiles",
                "RutaJson",
                fabricante.ToUpper()
            );
        }

        // Método genérico para cargar una lista desde un archivo JSON.
        // Si hay cualquier problema, devuelve lista vacía para evitar errores.
        private List<T> CargarListaDesdeJson<T>(string rutaArchivo)
        {
            try
            {
                // Si el archivo no existe, devuelve lista vacía.
                if (!File.Exists(rutaArchivo))
                    return new List<T>();

                string json = File.ReadAllText(rutaArchivo);

                // Si el contenido está vacío, devuelve lista vacía.
                if (string.IsNullOrWhiteSpace(json))
                    return new List<T>();

                // Deserializa el JSON a lista del tipo indicado.
                List<T> resultado = JsonConvert.DeserializeObject<List<T>>(json);

                return resultado ?? new List<T>();
            }
            catch
            {
                // En caso de error (lectura o parseo), no rompe la app.
                return new List<T>();
            }
        }

        // Carga las planchas disponibles del fabricante.
        public List<PosiblePlanchaModel> CargarPlanchas(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesPlanchas.json"
            );

            return CargarListaDesdeJson<PosiblePlanchaModel>(ruta);
        }

        // Carga las configuraciones de unión disponibles.
        public List<PosibleConfiguracionModel> CargarConfiguraciones(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesConfiguraciones.json"
            );

            return CargarListaDesdeJson<PosibleConfiguracionModel>(ruta);
        }

        // Carga los rigidizadores disponibles.
        public List<PosibleRigidizadorModel> CargarRigidizadores(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesRigidizadores.json"
            );

            return CargarListaDesdeJson<PosibleRigidizadorModel>(ruta);
        }

        // Carga los starter rings disponibles.
        public List<PosibleStarterRingModel> CargarStarterRings(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesSR.json"
            );

            return CargarListaDesdeJson<PosibleStarterRingModel>(ruta);
        }

        // Carga los tornillos disponibles.
        public List<PosibleTornilloModel> CargarTornillos(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesTornillos.json"
            );

            return CargarListaDesdeJson<PosibleTornilloModel>(ruta);
        }
    }
}