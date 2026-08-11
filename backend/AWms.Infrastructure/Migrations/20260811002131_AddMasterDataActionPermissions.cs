using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataActionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "Code", "ModuleCode", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000014"), "ACTION", "action.warehouse.create", "master-data", "创建仓库" },
                    { new Guid("10000000-0000-0000-0000-000000000015"), "ACTION", "action.warehouse.edit", "master-data", "编辑仓库" },
                    { new Guid("10000000-0000-0000-0000-000000000016"), "ACTION", "action.warehouse.delete", "master-data", "删除仓库" },
                    { new Guid("10000000-0000-0000-0000-000000000017"), "ACTION", "action.location.create", "master-data", "创建库位" },
                    { new Guid("10000000-0000-0000-0000-000000000018"), "ACTION", "action.location.edit", "master-data", "编辑库位" },
                    { new Guid("10000000-0000-0000-0000-000000000019"), "ACTION", "action.location.delete", "master-data", "删除库位" },
                    { new Guid("10000000-0000-0000-0000-000000000020"), "ACTION", "action.source.create", "master-data", "创建来源" },
                    { new Guid("10000000-0000-0000-0000-000000000021"), "ACTION", "action.source.edit", "master-data", "编辑来源" },
                    { new Guid("10000000-0000-0000-0000-000000000022"), "ACTION", "action.source.delete", "master-data", "删除来源" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000041"), new Guid("10000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000042"), new Guid("10000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000043"), new Guid("10000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000044"), new Guid("10000000-0000-0000-0000-000000000017"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000045"), new Guid("10000000-0000-0000-0000-000000000018"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000046"), new Guid("10000000-0000-0000-0000-000000000019"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000047"), new Guid("10000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000048"), new Guid("10000000-0000-0000-0000-000000000021"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000049"), new Guid("10000000-0000-0000-0000-000000000022"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000050"), new Guid("10000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000051"), new Guid("10000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000052"), new Guid("10000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000053"), new Guid("10000000-0000-0000-0000-000000000017"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000054"), new Guid("10000000-0000-0000-0000-000000000018"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000055"), new Guid("10000000-0000-0000-0000-000000000019"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000056"), new Guid("10000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000057"), new Guid("10000000-0000-0000-0000-000000000021"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000058"), new Guid("10000000-0000-0000-0000-000000000022"), new Guid("00000000-0000-0000-0000-000000000001") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000041"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000042"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000043"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000044"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000045"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000046"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000056"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000057"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000058"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000022"));
        }
    }
}
