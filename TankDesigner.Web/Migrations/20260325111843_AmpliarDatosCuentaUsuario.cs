using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankDesigner.Web.Migrations
{
    /// <inheritdoc />
    public partial class AmpliarDatosCuentaUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "NombreCompleto",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cargo",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaCiudad",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaCodigoPostal",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaDireccion",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaIdentificacionFiscal",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaNombre",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaPais",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaProvincia",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaWeb",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonoContacto",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cargo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaCiudad",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaCodigoPostal",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaDireccion",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaIdentificacionFiscal",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaNombre",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaPais",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaProvincia",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmpresaWeb",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TelefonoContacto",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "NombreCompleto",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
