using Newtonsoft.Json;
using TankDesigner.Core.Models.Catalogos;

namespace TankDesigner.Infrastructure.Services
{
    public class CatalogoJsonService
    {
        private string ObtenerRutaBase()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                "Plantillas",
                "TankStructuralDesignerFiles",
                "RutaJson");
        }

        private string NormalizarFabricante(string fabricante)
        {
            if (string.IsNullOrWhiteSpace(fabricante))
                return string.Empty;

            string valor = fabricante.Trim().ToUpperInvariant();

            if (valor == "BALMORAL")
                return "BALMORAL";

            if (valor == "PERMASTORE")
                return "PERMASTORE";

            if (valor == "DL2")
                return "DL2";

            return valor;
        }

        private string ObtenerRutaFabricante(string fabricante)
        {
            string carpeta = NormalizarFabricante(fabricante);

            return Path.Combine(
                ObtenerRutaBase(),
                carpeta);
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

        public List<PosibleConfiguracionModel> CargarConfiguraciones(string fabricante)
        {
            string ruta = Path.Combine(
                ObtenerRutaFabricante(fabricante),
                "ListaPosiblesConfiguraciones.json");

            return CargarListaDesdeJson<PosibleConfiguracionModel>(ruta);
        }

        public List<string> ObtenerModelosDisponibles(string fabricante)
        {
            return CargarConfiguraciones(fabricante)
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Nombre))
                .Select(x => x.Nombre.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        public bool ExisteCatalogoFabricante(string fabricante)
        {
            string ruta = ObtenerRutaFabricante(fabricante);
            return Directory.Exists(ruta);
        }
    }
}