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
                resultado.AccionesRecomendadas.Add("Seleccionar una ubicación o rellenar manualmente las condiciones de obra si se quiere documentar el emplazamiento.");
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
            resultado.PuntuacionInstalacion = CalcularPuntuacion(resultado);
            resultado.ComplejidadGlobal = CalcularComplejidadGlobal(resultado.PuntuacionInstalacion);

            resultado.FactoresDetectados = ObtenerFactoresDetectados(emplazamiento, resultado, entorno, viento, acceso, terreno, ambiente);
            resultado.Recomendaciones = ObtenerRecomendaciones(resultado, entorno, acceso, terreno, ambiente).Distinct().ToList();
            resultado.AccionesRecomendadas = ObtenerAcciones(resultado, entorno, acceso, terreno, ambiente).Distinct().ToList();
            resultado.RecomendacionPrincipal = resultado.Recomendaciones.FirstOrDefault()
                ?? "No se detectan advertencias relevantes con los datos de emplazamiento indicados.";
            resultado.ResumenProfesional = CrearResumenProfesional(emplazamiento, resultado);

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

        private static int CalcularPuntuacion(AnalisisEmplazamientoResultadoModel resultado)
        {
            int puntos = 100;
            puntos -= Penalizacion(resultado.RiesgoViento);
            puntos -= Penalizacion(resultado.RiesgoCorrosion);
            puntos -= Penalizacion(resultado.DificultadMontaje);
            puntos -= Penalizacion(resultado.ImpactoTransporte);
            return Math.Clamp(puntos, 15, 100);
        }

        private static int Penalizacion(string valor)
        {
            string texto = Normalizar(valor);
            if (texto == "alto" || texto == "alta") return 22;
            if (texto == "medio" || texto == "media") return 11;
            if (texto == "no evaluado" || texto == "no evaluada") return 4;
            return 0;
        }

        private static string CalcularComplejidadGlobal(int puntuacion)
        {
            if (puntuacion >= 82) return "Baja";
            if (puntuacion >= 58) return "Media";
            return "Alta";
        }

        private static List<string> ObtenerFactoresDetectados(
            EmplazamientoInstalacionModel emplazamiento,
            AnalisisEmplazamientoResultadoModel resultado,
            string entorno,
            string viento,
            string acceso,
            string terreno,
            string ambiente)
        {
            var factores = new List<string>();

            if (!string.IsNullOrWhiteSpace(emplazamiento.Ciudad) || !string.IsNullOrWhiteSpace(emplazamiento.Provincia))
                factores.Add($"Ubicación identificada: {TextoUbicacion(emplazamiento)}.");

            if (!string.IsNullOrWhiteSpace(emplazamiento.FuenteDatos))
                factores.Add($"Datos de mapa obtenidos mediante {emplazamiento.FuenteDatos}.");

            if (!string.IsNullOrWhiteSpace(entorno))
                factores.Add($"Entorno declarado: {emplazamiento.TipoEntorno}.");

            if (!string.IsNullOrWhiteSpace(viento))
                factores.Add($"Exposición al viento: {emplazamiento.ExposicionViento}.");

            if (!string.IsNullOrWhiteSpace(acceso))
                factores.Add($"Acceso a obra: {emplazamiento.AccesoObra}.");

            if (!string.IsNullOrWhiteSpace(terreno))
                factores.Add($"Tipo de terreno: {emplazamiento.TipoTerreno}.");

            if (!string.IsNullOrWhiteSpace(ambiente))
                factores.Add($"Ambiente previsto: {emplazamiento.Ambiente}.");

            factores.Add($"Complejidad global estimada: {resultado.ComplejidadGlobal} ({resultado.PuntuacionInstalacion}/100).");
            return factores;
        }

        private static List<string> ObtenerRecomendaciones(
            AnalisisEmplazamientoResultadoModel resultado,
            string entorno,
            string acceso,
            string terreno,
            string ambiente)
        {
            var recomendaciones = new List<string>();

            if (resultado.RiesgoViento == "Alto")
                recomendaciones.Add("Revisar cargas de viento, rigidizador superior, anclajes y condiciones de montaje antes de validar el diseño final.");
            else if (resultado.RiesgoViento == "Medio")
                recomendaciones.Add("Comprobar que la exposición al viento usada en el cálculo es coherente con el emplazamiento seleccionado.");

            if (resultado.RiesgoCorrosion == "Alto")
                recomendaciones.Add("Considerar protección anticorrosiva reforzada, revisión de recubrimientos y plan de mantenimiento periódico.");
            else if (resultado.RiesgoCorrosion == "Medio")
                recomendaciones.Add("Revisar sistema de protección superficial y condiciones ambientales previstas durante la vida útil del tanque.");

            if (resultado.DificultadMontaje == "Alta")
                recomendaciones.Add("Planificar con detalle acceso de maquinaria, grúa, zona de acopio, seguridad y secuencia de montaje.");
            else if (resultado.DificultadMontaje == "Media")
                recomendaciones.Add("Confirmar espacio disponible para montaje, acopio de chapas y maniobra de equipos auxiliares.");

            if (resultado.ImpactoTransporte == "Alto")
                recomendaciones.Add("Revisar rutas de transporte, restricciones de acceso, radios de giro y posibilidad de descarga en obra.");
            else if (resultado.ImpactoTransporte == "Medio")
                recomendaciones.Add("Confirmar distancia real a obra y condiciones de descarga para evitar desviaciones de plazo.");

            if (entorno == "urbano")
                recomendaciones.Add("Revisar permisos, horarios de trabajo, ruido, cortes de acceso y espacio real de montaje.");

            if (entorno == "industrial")
                recomendaciones.Add("Coordinar instalación con seguridad industrial, accesos internos, permisos de trabajo y circulación de maquinaria.");

            if (entorno == "rural")
                recomendaciones.Add("Verificar acceso de camiones, firme de caminos, disponibilidad de grúa y distancia a servicios auxiliares.");

            if (terreno == "irregular" || terreno == "blando" || terreno == "elevado")
                recomendaciones.Add("Revisar cimentación, nivelación, estabilidad del terreno y zona segura para elevación de cargas.");

            if (ambiente == "industrial agresivo")
                recomendaciones.Add("Validar compatibilidad de recubrimientos y juntas con el ambiente industrial previsto.");

            if (acceso == "dificil" || acceso == "difícil")
                recomendaciones.Add("Valorar incremento de tiempo de montaje por restricciones de acceso y maniobra.");

            if (recomendaciones.Count == 0)
                recomendaciones.Add("No se detectan advertencias relevantes con los datos de emplazamiento indicados.");

            return recomendaciones;
        }

        private static List<string> ObtenerAcciones(
            AnalisisEmplazamientoResultadoModel resultado,
            string entorno,
            string acceso,
            string terreno,
            string ambiente)
        {
            var acciones = new List<string>
            {
                "Confirmar in situ el espacio disponible para acopio de paneles, tornillería y equipos auxiliares.",
                "Comprobar que el acceso de camión y grúa coincide con lo previsto antes de cerrar la planificación."
            };

            if (resultado.RiesgoViento == "Alto" || resultado.RiesgoViento == "Medio")
                acciones.Add("Revisar los criterios de viento antes de emitir el informe final o una oferta definitiva.");

            if (resultado.RiesgoCorrosion == "Alto" || ambiente == "industrial agresivo")
                acciones.Add("Solicitar confirmación de ambiente corrosivo para ajustar protección superficial y mantenimiento.");

            if (terreno == "blando" || terreno == "irregular" || terreno == "elevado")
                acciones.Add("Validar cimentación, nivelación y capacidad portante antes del montaje.");

            if (acceso == "dificil" || acceso == "difícil")
                acciones.Add("Preparar plan específico de maniobras, descarga y circulación de maquinaria.");

            if (entorno == "urbano")
                acciones.Add("Comprobar permisos municipales, ruido, cortes de acceso y limitaciones horarias.");

            if (entorno == "industrial")
                acciones.Add("Coordinar permisos de trabajo, PRL, accesos internos y señalización de la zona.");

            return acciones;
        }

        private static string CrearResumenProfesional(EmplazamientoInstalacionModel emplazamiento, AnalisisEmplazamientoResultadoModel resultado)
        {
            var ubicacion = TextoUbicacion(emplazamiento);
            var entorno = string.IsNullOrWhiteSpace(emplazamiento.TipoEntorno) ? "sin entorno clasificado" : $"entorno {emplazamiento.TipoEntorno.ToLowerInvariant()}";
            var viento = string.IsNullOrWhiteSpace(emplazamiento.ExposicionViento) ? "sin exposición al viento indicada" : $"exposición al viento {emplazamiento.ExposicionViento.ToLowerInvariant()}";

            return $"El tanque se ha asociado al emplazamiento {ubicacion}, con {entorno} y {viento}. " +
                   $"La complejidad global estimada de instalación es {resultado.ComplejidadGlobal.ToLowerInvariant()} " +
                   $"({resultado.PuntuacionInstalacion}/100). Este análisis es orientativo y no modifica el cálculo estructural ni el presupuesto base.";
        }

        private static string TextoUbicacion(EmplazamientoInstalacionModel emplazamiento)
        {
            if (!string.IsNullOrWhiteSpace(emplazamiento.NombreUbicacion))
                return emplazamiento.NombreUbicacion.Trim();

            var partes = new[] { emplazamiento.Ciudad, emplazamiento.Provincia, emplazamiento.Pais }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (partes.Count > 0)
                return string.Join(", ", partes);

            if (emplazamiento.Latitud.HasValue && emplazamiento.Longitud.HasValue)
                return $"coordenadas {emplazamiento.Latitud.Value:0.######}, {emplazamiento.Longitud.Value:0.######}";

            return "indicado manualmente";
        }

        private static string Normalizar(string? texto)
        {
            return string.IsNullOrWhiteSpace(texto)
                ? string.Empty
                : texto.Trim().ToLowerInvariant();
        }
    }
}
