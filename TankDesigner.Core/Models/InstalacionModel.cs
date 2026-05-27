namespace TankDesigner.Core.Models
{
    public class InstalacionModel
    {
        public string TipoMedioAnillo { get; set; } = "Anillo entero";
        public bool StarterRing { get; set; } = true;
        public string TipoTecho { get; set; } = "Sin techo";

        public string TipoEscalera { get; set; } = "Sin escalera";
        public int NumeroEscaleras { get; set; } = 0;

        public int ConexionesDN25_DN150 { get; set; }
        public int ConexionesDN150_DN300 { get; set; }
        public int ConexionesDN300_DN500 { get; set; }
        public int ConexionesMayorDN500 { get; set; }
        public int NumeroBocasHombre { get; set; } = 1;

        public int TamanoCuadrilla { get; set; } = 4;
        public double HorasTrabajoDia { get; set; } = 8;
        public double DiasLluviaPorcentaje { get; set; } = 10;

        public int SiteManager { get; set; } = 1;
        public int TecnicoSeguridad { get; set; } = 1;

        public string LugarObra { get; set; } = "Nacional";
        public double DistanciaAlojamientoObra { get; set; } = 0;
        public double CosteTransporteManual { get; set; } = 0;

        // Mejora opcional: ubicación/emplazamiento de instalación.
        // Si no se rellena, no afecta a cálculos, presupuesto ni validaciones principales.
        public EmplazamientoInstalacionModel Emplazamiento { get; set; } = new();
    }

    public class EmplazamientoInstalacionModel
    {
        public bool UbicacionSeleccionada { get; set; } = false;

        public string NombreUbicacion { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Provincia { get; set; } = string.Empty;
        public string Pais { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public string DireccionResumen { get; set; } = string.Empty;
        public string FuenteDatos { get; set; } = string.Empty;
        public string FechaConsulta { get; set; } = string.Empty;

        public double? Latitud { get; set; }
        public double? Longitud { get; set; }

        public string TipoEntorno { get; set; } = string.Empty;
        public string ExposicionViento { get; set; } = string.Empty;
        public string AccesoObra { get; set; } = string.Empty;
        public string TipoTerreno { get; set; } = string.Empty;
        public string Ambiente { get; set; } = string.Empty;
        public string ObservacionesInstalacion { get; set; } = string.Empty;

        public bool TieneDatos =>
            UbicacionSeleccionada
            || Latitud.HasValue
            || Longitud.HasValue
            || !string.IsNullOrWhiteSpace(NombreUbicacion)
            || !string.IsNullOrWhiteSpace(Ciudad)
            || !string.IsNullOrWhiteSpace(Provincia)
            || !string.IsNullOrWhiteSpace(Pais)
            || !string.IsNullOrWhiteSpace(CodigoPostal)
            || !string.IsNullOrWhiteSpace(DireccionResumen)
            || !string.IsNullOrWhiteSpace(TipoEntorno)
            || !string.IsNullOrWhiteSpace(ExposicionViento)
            || !string.IsNullOrWhiteSpace(AccesoObra)
            || !string.IsNullOrWhiteSpace(TipoTerreno)
            || !string.IsNullOrWhiteSpace(Ambiente)
            || !string.IsNullOrWhiteSpace(ObservacionesInstalacion);
    }

    public class AnalisisEmplazamientoResultadoModel
    {
        public string RiesgoViento { get; set; } = "No evaluado";
        public string RiesgoCorrosion { get; set; } = "No evaluado";
        public string DificultadMontaje { get; set; } = "No evaluada";
        public string ImpactoTransporte { get; set; } = "No evaluado";
        public string ComplejidadGlobal { get; set; } = "No evaluada";
        public int PuntuacionInstalacion { get; set; } = 0;
        public string RecomendacionPrincipal { get; set; } = "Completa la ubicación o las condiciones de instalación para obtener recomendaciones.";
        public string ResumenProfesional { get; set; } = "El análisis de emplazamiento es opcional y no modifica el cálculo ni el presupuesto.";
        public List<string> Recomendaciones { get; set; } = new();
        public List<string> AccionesRecomendadas { get; set; } = new();
        public List<string> FactoresDetectados { get; set; } = new();
    }
}
