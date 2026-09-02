using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AzureSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pacs008Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    EndToEndId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    CreationDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DebtorName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    DebtorIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    DebtorBic = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    CreditorName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreditorIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    CreditorBic = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    RemittanceInformation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pacs008Messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pacs008Messages_MessageId",
                table: "Pacs008Messages",
                column: "MessageId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pacs008Messages");
        }
    }
}
