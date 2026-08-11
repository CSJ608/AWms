using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuRequiredPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "RequiredPermissionCode",
                value: "route.inbound");

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "RequiredPermissionCode",
                value: "route.master-data");

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "RequiredPermissionCode",
                value: "route.system");

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "RequiredPermissionCode",
                value: "route.inbound");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "RequiredPermissionCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "RequiredPermissionCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "RequiredPermissionCode",
                value: null);

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "RequiredPermissionCode",
                value: null);
        }
    }
}
