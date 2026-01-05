using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Obrasci.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PendingPackage",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingPackageEffectiveDate",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingPackage",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PendingPackageEffectiveDate",
                table: "AspNetUsers");
        }
    }
}
