using TankDesigner.Core.Models;

namespace TankDesigner.Web.Services
{
    public class FormularioValidacionService
    {
        public List<string> ValidarProyecto(ProyectoGeneralModel proyecto)
        {
            List<string> errores = new();

            if (proyecto == null)
            {
                errores.Add("No hay datos de proyecto.");
                return errores;
            }

            if (string.IsNullOrWhiteSpace(proyecto.Normativa))
                errores.Add("La normativa es obligatoria.");

            if (string.IsNullOrWhiteSpace(proyecto.Fabricante))
                errores.Add("El fabricante es obligatorio.");

            if (string.IsNullOrWhiteSpace(proyecto.MaterialPrincipal))
                errores.Add("El material principal es obligatorio.");

            if (string.IsNullOrWhiteSpace(proyecto.ModeloCalculo))
                errores.Add("El modelo de cálculo es obligatorio.");

            return errores;
        }

        public List<string> ValidarGeometria(TankModel tanque)
        {
            List<string> errores = new();

            if (tanque == null)
            {
                errores.Add("No hay datos geométricos.");
                return errores;
            }

            if (tanque.ChapasPorAnillo <= 0)
                errores.Add("El número de chapas por anillo debe ser mayor que 0.");

            if (tanque.NumeroAnillos <= 0)
                errores.Add("El número de anillos debe ser mayor que 0.");

            if (tanque.AnilloArranque <= 0)
                errores.Add("El anillo de arranque debe ser mayor que 0.");

            if (tanque.AnilloArranque > tanque.NumeroAnillos)
                errores.Add("El anillo de arranque no puede ser mayor que el número de anillos.");

            if (tanque.BordeLibre < 0)
                errores.Add("El borde libre no puede ser negativo.");

            if (tanque.DensidadLiquido <= 0)
                errores.Add("La densidad del líquido debe ser mayor que 0.");


            return errores;
        }

        public List<string> ValidarCargas(CargasModel cargas)
        {
            List<string> errores = new();

            if (cargas == null)
            {
                errores.Add("No hay datos de cargas.");
                return errores;
            }

            if (cargas.VelocidadViento < 0)
                errores.Add("La velocidad de viento no puede ser negativa.");

            if (cargas.SnowLoad < 0)
                errores.Add("La carga de nieve no puede ser negativa.");

            if (cargas.RoofDeadLoad < 0 || cargas.RoofSnowLoad < 0 || cargas.RoofLiveLoad < 0)
                errores.Add("Las cargas de cubierta no pueden ser negativas.");

            if (cargas.RoofCentroid < 0 || cargas.RoofProjectedArea < 0)
                errores.Add("Los valores geométricos de cubierta no pueden ser negativos.");

            bool techoNone = string.IsNullOrWhiteSpace(cargas.RoofType)
                             || cargas.RoofType.Equals("None", StringComparison.OrdinalIgnoreCase);

            if (techoNone &&
                (cargas.RoofDeadLoad != 0 ||
                 cargas.RoofSnowLoad != 0 ||
                 cargas.RoofLiveLoad != 0 ||
                 cargas.RoofCentroid != 0 ||
                 cargas.RoofProjectedArea != 0 ||
                 cargas.SnowLoad != 0))
            {
                errores.Add("Si el techo está en None, todas las cargas y geometrías de cubierta deben ser 0.");
            }

            if (cargas.Ss < 0 || cargas.S1 < 0 || cargas.TL < 0)
                errores.Add("Los parámetros sísmicos no pueden ser negativos.");

            return errores;
        }

        public List<string> ValidarInstalacion(InstalacionModel instalacion)
        {
            List<string> errores = new();

            if (instalacion == null)
            {
                errores.Add("No hay datos de instalación.");
                return errores;
            }

            if (instalacion.NumeroEscaleras < 0)
                errores.Add("El número de escaleras no puede ser negativo.");

            if (instalacion.ConexionesDN25_DN150 < 0 ||
                instalacion.ConexionesDN150_DN300 < 0 ||
                instalacion.ConexionesDN300_DN500 < 0 ||
                instalacion.ConexionesMayorDN500 < 0)
                errores.Add("Las conexiones no pueden ser negativas.");

            if (instalacion.TamanoCuadrilla < 0 ||
                instalacion.HorasTrabajoDia < 0 ||
                instalacion.DiasLluviaPorcentaje < 0 ||
                instalacion.SiteManager < 0 ||
                instalacion.TecnicoSeguridad < 0 ||
                instalacion.DistanciaAlojamientoObra < 0)
                errores.Add("Los valores de instalación no pueden ser negativos.");

            return errores;
        }

        public List<string> ValidarTodo(
            ProyectoGeneralModel proyecto,
            TankModel tanque,
            CargasModel cargas,
            InstalacionModel instalacion)
        {
            List<string> errores = new();
            errores.AddRange(ValidarProyecto(proyecto));
            errores.AddRange(ValidarGeometria(tanque));
            errores.AddRange(ValidarCargas(cargas));
            errores.AddRange(ValidarInstalacion(instalacion));
            return errores;
        }
    }
}
