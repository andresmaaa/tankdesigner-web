namespace TankDesigner.Core.Models
{
    public class InstalacionModel
    {
        public string TipoMedioAnillo { get; set; } = "No";
        public bool StarterRing { get; set; } = true;
        public string TipoTecho { get; set; } = "Sin techo";

        public string TipoEscalera { get; set; } = "Sin escalera";
        public int NumeroEscaleras { get; set; } = 0;

        public int ConexionesDN25_DN150 { get; set; }
        public int ConexionesDN150_DN300 { get; set; }
        public int ConexionesDN300_DN500 { get; set; }
        public int ConexionesMayorDN500 { get; set; }

        public int TamanoCuadrilla { get; set; } = 4;
        public double HorasTrabajoDia { get; set; } = 8;
        public double DiasLluviaPorcentaje { get; set; } = 10;

        public int SiteManager { get; set; } = 1;
        public int TecnicoSeguridad { get; set; } = 1;

        public string LugarObra { get; set; } = "Nacional";
        public double DistanciaAlojamientoObra { get; set; } = 0;
    }
}