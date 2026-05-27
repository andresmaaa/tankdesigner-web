using TankDesigner.Core.Models;

namespace TankDesigner.Core.Services
{
    public class AnalisisEmplazamientoService
    {
        public AnalisisEmplazamientoResultadoModel Analizar(EmplazamientoInstalacionModel? emplazamiento)
        {
            var resultado = new AnalisisEmplazamientoResultadoModel();

            if (emplazamiento == null || !emplazamiento.TieneDatos)
            {
                resultado.Recomendaciones.Add("El análisis de emplazamiento es opcional. Si no se completa, no modifica el cálculo ni el presupuesto.");
                return resultado;
            }

            string entorno = Normalizar(emplazamiento.TipoEntorno);
            string viento = Normalizar(emplazamiento.ExposicionViento);
            string acceso = Normalizar(emplazamiento.AccesoObra);
            string terreno = Normalizar(emplazamiento.TipoTerreno);
            string ambiente = Normalizar(emplazamiento.Ambiente);

            resultado.RiesgoViento = CalcularRiesgoViento(viento, entorno, terreno);
            resultado.RiesgoCorrosion = CalcularRiesgoCorrosion(entorno, ambiente);
            resultado.DificultadMontaje = CalcularDificultadMontaje(acceso, terreno, entorno);
            resultado.ImpactoTransporte = CalcularImpactoTransporte(acceso, entorno, terreno);

            var recomendaciones = new List<string>();

            if (resultado.RiesgoViento == "Alto")
                recomendaciones.Add("Revisar las cargas de viento, el rigidizador superior y las condiciones de anclaje antes de validar el diseño final.");
            else if (resultado.RiesgoViento == "Medio")
                recomendaciones.Add("Comprobar que la exposición al viento usada en el cálculo es coherente con el emplazamiento seleccionado.");

            if (resultado.RiesgoCorrosion == "Alto")
                recomendaciones.Add("Considerar protección anticorrosiva reforzada, mantenimiento periódico y revisión de recubrimientos.");
            else if (resultado.RiesgoCorrosion == "Medio")
                recomendaciones.Add("Revisar el sistema de protección superficial y las condiciones ambientales previstas.");

            if (resultado.DificultadMontaje == "Alta")
                recomendaciones.Add("Planificar con detalle el acceso de maquinaria, zona de acopio, grúa y seguridad de montaje.");
            else if (resultado.DificultadMontaje == "Media")
                recomendaciones.Add("Confirmar espacio disponible para montaje, acopio de chapas y maniobra de equipos auxiliares.");

            if (entorno == "urbano")
                recomendaciones.Add("Revisar permisos, horarios de trabajo, ruido, cortes de acceso y espacio real de montaje.");

            if (entorno == "industrial")
                recomendaciones.Add("Coordinar la instalación con seguridad industrial, accesos internos, permisos de trabajo y circulación de maquinaria.");

            if (entorno == "rural")
                recomendaciones.Add("Verificar acceso de camiones, disponibilidad de grúa, firme de caminos y distancia a servicios auxiliares.");

            if (terreno == "irregular" || terreno == "blando" || terreno == "elevado")
                recomendaciones.Add("Revisar cimentación, nivelación, estabilidad del terreno y zona segura para elevación de cargas.");

            if (acceso == "dificil" || acceso == "difícil")
                recomendaciones.Add("Valorar incremento de tiempo de montaje por restricciones de acceso y maniobra.");

            if (recomendaciones.Count == 0)
                recomendaciones.Add("No se detectan advertencias relevantes con los datos de emplazamiento indicados.");

            resultado.Recomendaciones = recomendaciones.Distinct().ToList();
            resultado.RecomendacionPrincipal = resultado.Recomendaciones.First();

            return resultado;
        }

        private static string CalcularRiesgoViento(string viento, string entorno, string terreno)
        {
            if (viento == "muy expuesta" || terreno == "elevado")
                return "Alto";

            if (viento == "abierta" || entorno == "costero")
                return "Medio";

            if (viento == "protegida")
                return "Bajo";

            return string.IsNullOrWhiteSpace(viento) ? "No evaluado" : "Bajo";
        }

        private static string CalcularRiesgoCorrosion(string entorno, string ambiente)
        {
            if (entorno == "costero" || ambiente == "costero/corrosivo" || ambiente == "industrial agresivo")
                return "Alto";

            if (ambiente == "exterior normal" || entorno == "industrial")
                return "Medio";

            if (ambiente == "interior seco")
                return "Bajo";

            return string.IsNullOrWhiteSpace(ambiente) && string.IsNullOrWhiteSpace(entorno) ? "No evaluado" : "Bajo";
        }

        private static string CalcularDificultadMontaje(string acceso, string terreno, string entorno)
        {
            if (acceso == "dificil" || acceso == "difícil" || terreno == "blando" || terreno == "elevado")
                return "Alta";

            if (acceso == "medio" || terreno == "irregular" || entorno == "urbano")
                return "Media";

            if (acceso == "bueno" || terreno == "normal")
                return "Baja";

            return string.IsNullOrWhiteSpace(acceso) && string.IsNullOrWhiteSpace(terreno) && string.IsNullOrWhiteSpace(entorno)
                ? "No evaluada"
                : "Media";
        }

        private static string CalcularImpactoTransporte(string acceso, string entorno, string terreno)
        {
            if (acceso == "dificil" || acceso == "difícil" || terreno == "blando")
                return "Alto";

            if (acceso == "medio" || entorno == "rural" || terreno == "irregular")
                return "Medio";

            if (acceso == "bueno")
                return "Bajo";

            return string.IsNullOrWhiteSpace(acceso) && string.IsNullOrWhiteSpace(entorno) && string.IsNullOrWhiteSpace(terreno)
                ? "No evaluado"
                : "Medio";
        }

        private static string Normalizar(string? texto)
        {
            return string.IsNullOrWhiteSpace(texto)
                ? string.Empty
                : texto.Trim().ToLowerInvariant();
        }
    }
}
