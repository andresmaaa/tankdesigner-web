using System.Collections.Generic;

namespace TankDesigner.Core.Models.Presupuestos
{
    public sealed class PresupuestoConfigJsonModel
    {
        public ProductividadPresupuestoJsonModel Productividad { get; set; } = new();
        public CostesManoObraPresupuestoJsonModel CostesManoObra { get; set; } = new();
        public MediosAuxiliaresPresupuestoJsonModel MediosAuxiliares { get; set; } = new();
        public List<PanelFabricantePresupuestoJsonModel> PanelesFabricante { get; set; } = new();
        public decimal DensidadAcero { get; set; }
        public List<TechoPresupuestoJsonModel> Techo { get; set; } = new();
        public List<EscaleraPresupuestoJsonModel> Escaleras { get; set; } = new();
        public TransportePresupuestoJsonModel Transporte { get; set; } = new();
        public VuelosPresupuestoJsonModel Vuelos { get; set; } = new();
        public PesosPresupuestoJsonModel Pesos { get; set; } = new();
    }

    public sealed class ProductividadPresupuestoJsonModel
    {
        public decimal HorasPorPlacaPersona { get; set; }
        public decimal HorasPor100kgPlacaSinGrua { get; set; }
        public decimal HorasPor100kgPlacaConGrua { get; set; }
        public decimal PanelesPorDiaCamionGrua { get; set; }
        public decimal UmbralPesoPanelGrua { get; set; }
        public decimal HorasPorRigidizador { get; set; }
        public decimal HorasSelladoMetro { get; set; }
        public decimal HorasEscaleraVerticalMetro { get; set; }
        public decimal HorasEscaleraHelicoidalMetro { get; set; }
        public HorasConexionPresupuestoJsonModel HorasConexion { get; set; } = new();
        public decimal HorasStarterRing { get; set; }
        public decimal HorasAnclajeMetro { get; set; }
        public decimal HorasBocaHombre { get; set; }
        public decimal HorasCambioGato { get; set; }
    }

    public sealed class HorasConexionPresupuestoJsonModel
    {
        public decimal DN25_DN150 { get; set; }
        public decimal DN150_DN300 { get; set; }
        public decimal DN300_DN500 { get; set; }
        public decimal DN500 { get; set; }
    }

    public sealed class CostesManoObraPresupuestoJsonModel
    {
        public decimal OperarioNacional { get; set; }
        public decimal IngenieroNacional { get; set; }
        public decimal TecnicoSeguridadNacional { get; set; }
        public decimal TrabajadorInternacional { get; set; }
        public decimal IngenieroInternacional { get; set; }
        public decimal TecnicoSeguridadInternacional { get; set; }
    }

    public sealed class MediosAuxiliaresPresupuestoJsonModel
    {
        public decimal CamionGruaDia { get; set; }
        public decimal AlquilerGatosDia { get; set; }
        public decimal VehiculoAlquilerDia { get; set; }
        public decimal ConsumiblesPanel { get; set; }
        public decimal ConsumiblesTechoMetro { get; set; }
        public decimal TripodesPorMetro { get; set; }
        public decimal ConsumiblesTechoM2 { get; set; }
    }

    public sealed class PanelFabricantePresupuestoJsonModel
    {
        public string Fabricante { get; set; } = string.Empty;
        public decimal AltoPanel { get; set; }
        public decimal LargoPanel { get; set; }
        public decimal PesoKgM2 { get; set; }
    }

    public sealed class TechoPresupuestoJsonModel
    {
        public string Tipo { get; set; } = string.Empty;
        public decimal HorasEstructuraM2 { get; set; }
        public decimal HorasPanelM2 { get; set; }
        public decimal FactorIzados { get; set; }
        public decimal PrecioSuministro { get; set; }
    }

    public sealed class EscaleraPresupuestoJsonModel
    {
        public string Tipo { get; set; } = string.Empty;
        public decimal PrecioMetro { get; set; }
    }

    public sealed class TransportePresupuestoJsonModel
    {
        public decimal Nacional { get; set; }
        public decimal Europa { get; set; }
        public decimal Internacional { get; set; }
        public decimal PesoMaximoContenedor { get; set; }
    }

    public sealed class VuelosPresupuestoJsonModel
    {
        public decimal Nacional { get; set; }
        public decimal Europa { get; set; }
        public decimal Internacional { get; set; }
    }

    public sealed class PesosPresupuestoJsonModel
    {
        public decimal EscaleraVerticalKgMetro { get; set; }
        public decimal EscaleraHelicoidalKgMetro { get; set; }
    }
}