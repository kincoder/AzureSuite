using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_MessageTypes_Name_Version",
                table: "MessageTypes",
                columns: new[] { "Name", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_MessageTypeName_MessageTypeVersion",
                table: "Routes",
                columns: new[] { "MessageTypeName", "MessageTypeVersion" });

            migrationBuilder.AddForeignKey(
                name: "FK_Routes_Clients_ClientId",
                table: "Routes",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Routes_MessageTypes_MessageTypeName_MessageTypeVersion",
                table: "Routes",
                columns: new[] { "MessageTypeName", "MessageTypeVersion" },
                principalTable: "MessageTypes",
                principalColumns: new[] { "Name", "Version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Routes_Clients_ClientId",
                table: "Routes");

            migrationBuilder.DropForeignKey(
                name: "FK_Routes_MessageTypes_MessageTypeName_MessageTypeVersion",
                table: "Routes");

            migrationBuilder.DropIndex(
                name: "IX_Routes_MessageTypeName_MessageTypeVersion",
                table: "Routes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MessageTypes_Name_Version",
                table: "MessageTypes");
        }
    }
}
