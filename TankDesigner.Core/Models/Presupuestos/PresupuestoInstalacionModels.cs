using System;
using System.Collections.Generic;
using System.Linq;

namespace TankDesigner.Core.Models.Presupuestos
{
    public enum FabricantePresupuesto
    {
        Balmoral,
        Permastore,
        DL2
    }

    public enum TipoTechoPresupuesto
    {
        SinTecho,
        Conico,
        Plano,
        DomoGeodesico
    }

    public enum TipoEscaleraPresupuesto
    {
        SinEscalera,
        Vertical,
        Helicoidal
    }

    public enum UbicacionObraPresupuesto
    {
        Nacional,
        Europa,
        Internacional
    }

    public sealed class PresupuestoInstalacionInputModel
    {
        // Geometría / configuración
        public FabricantePresupuesto Fabricante { get; set; }
        public int NumeroPlacasPorAnillo { get; set; }
        public int NumeroAnillos { get; set; }
        public bool TieneStarterRing { get; set; }
        public TipoTechoPresupuesto TipoTecho { get; set; }
        public TipoEscaleraPresupuesto TipoEscalera { get; set; }
        public int NumeroEscaleras { get; set; }

        // Conexiones
        public int ConexionesDn25a150 { get; set; }
        public int ConexionesDn150a300 { get; set; }
        public int ConexionesDn300a500 { get; set; }
        public int ConexionesMayor500 { get; set; }
        public int NumeroBocasHombre { get; set; } = 1;

        // Mano de obra
        public int TamanoCuadrilla { get; set; }
        public decimal HorasTrabajoPorDia { get; set; }
        public decimal PorcentajeLluvia { get; set; } // 0.10 = 10%
        public int NumeroSiteManagers { get; set; }
        public int NumeroTecnicosSeguridad { get; set; }
        public UbicacionObraPresupuesto UbicacionObra { get; set; }

        // Datos reales del tanque
        public decimal DiametroMetros { get; set; }
        public List<decimal> EspesoresAnillosMm { get; set; } = new();

        // Opcionales
        public decimal DistanciaAlojamientoObraHoras { get; set; }
        public decimal CosteTransporteManual { get; set; }

        public void Validar()
        {
            if (NumeroPlacasPorAnillo <= 0)
                throw new InvalidOperationException("NumeroPlacasPorAnillo debe ser mayor que 0.");

            if (NumeroAnillos <= 0)
                throw new InvalidOperationException("NumeroAnillos debe ser mayor que 0.");

            if (TamanoCuadrilla <= 0)
                throw new InvalidOperationException("TamanoCuadrilla debe ser mayor que 0.");

            if (HorasTrabajoPorDia <= 0)
                throw new InvalidOperationException("HorasTrabajoPorDia debe ser mayor que 0.");

            if (DiametroMetros <= 0)
                throw new InvalidOperationException("DiametroMetros debe ser mayor que 0.");

            if (EspesoresAnillosMm == null || EspesoresAnillosMm.Count == 0)
                throw new InvalidOperationException("Debes indicar los espesores reales de los anillos.");

            if (EspesoresAnillosMm.Count != NumeroAnillos)
                throw new InvalidOperationException("La lista de espesores debe tener un valor por cada anillo.");
        }
    }

    public sealed class PartidaPresupuestoModel
    {
        public string Codigo { get; set; } = string.Empty;
        public string Concepto { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
        public decimal Total { get; set; }
    }

    public sealed class HorasInstalacionDetalleModel
    {
        public decimal HorasMontajePlacas { get; set; }
        public decimal HorasCambiosGato { get; set; }
        public decimal HorasEscaleras { get; set; }
        public decimal HorasConexionesYBocaHombre { get; set; }
        public decimal HorasRigidizadores { get; set; }
        public decimal HorasAnclaje { get; set; }
        public decimal HorasSelladoCimentacionPared { get; set; }
        public decimal HorasTechoEstructura { get; set; }
        public decimal HorasTechoPaneles { get; set; }
        public decimal HorasDescanso { get; set; }
        public decimal HorasDesplazamiento { get; set; }
        public decimal HorasCamionGrua { get; set; }

        public decimal HorasTotalesMontajeDeposito =>
            HorasMontajePlacas +
            HorasCambiosGato +
            HorasEscaleras +
            HorasConexionesYBocaHombre +
            HorasRigidizadores +
            HorasAnclaje +
            HorasSelladoCimentacionPared;

        public decimal HorasTotalesTecho =>
            HorasTechoEstructura + HorasTechoPaneles;

        public decimal HorasTotalesGenerales =>
            HorasTotalesMontajeDeposito +
            HorasTotalesTecho +
            HorasDescanso +
            HorasDesplazamiento;
    }

    public sealed class CalendarioInstalacionModel
    {
        public decimal HorasEquipoTecho { get; set; }
        public decimal DiasTecho { get; set; }

        public decimal HorasEquipoDeposito { get; set; }
        public decimal DiasDeposito { get; set; }

        public decimal HorasEquipoDescanso { get; set; }
        public decimal DiasDescanso { get; set; }

        public decimal HorasEquipoDesplazamiento { get; set; }
        public decimal DiasDesplazamiento { get; set; }

        // Reproduce la hoja Excel (C39)
        public decimal DiasTotalesExcel { get; set; }

        // Dato más útil para mostrar en web
        public decimal DiasTotalesReales =>
            DiasTecho + DiasDeposito + DiasDescanso + DiasDesplazamiento;
    }

    public sealed class PresupuestoInstalacionResultadoModel
    {
        public decimal AlturaPanelMetros { get; set; }
        public decimal LongitudPanelMetros { get; set; }
        public decimal AlturaTanqueMetros { get; set; }
        public decimal AreaTechoM2 { get; set; }
        public decimal PerimetroTanqueMetros { get; set; }

        public HorasInstalacionDetalleModel Horas { get; set; } = new();
        public CalendarioInstalacionModel Calendario { get; set; } = new();
        public List<PartidaPresupuestoModel> Partidas { get; set; } = new();

        public decimal TotalInstalacion => Partidas.Sum(x => x.Total);
    }
}