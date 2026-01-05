using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Obrasci.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserActionLogs",
                table: "UserActionLogs");


            migrationBuilder.AddColumn<string>(
          name: "UserEmail",
          table: "UserActionLogs",
          type: "text",
          nullable: true);

            migrationBuilder.RenameColumn(
                name: "EntityType",
                table: "UserActionLogs",
                newName: "UserEmail");

            migrationBuilder.RenameColumn(
                name: "ActionName",
                table: "UserActionLogs",
                newName: "Action");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "UserActionLog",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserActionLog",
                table: "UserActionLog",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserActionLog",
                table: "UserActionLog");

           
            migrationBuilder.RenameColumn(
                name: "UserEmail",
                table: "UserActionLogs",
                newName: "EntityType");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "UserActionLogs",
                newName: "ActionName");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "UserActionLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "UserActionLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "UserActionLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserActionLogs",
                table: "UserActionLogs",
                column: "Id");
        }
    }
}
