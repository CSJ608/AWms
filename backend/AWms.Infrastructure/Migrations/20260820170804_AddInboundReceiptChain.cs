using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AWms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundReceiptChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "IdempotencyRecords",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PENDING");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BizType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BizId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    VoidedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BizType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BizId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Uom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockSubjects_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockSubjects_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockSubjects_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TxnGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TxnGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundOrderLines_InboundOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "InboundOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InboundOrderLines_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StagingLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceDocType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceDocNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SourceCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_InboundOrders_InboundOrderId",
                        column: x => x.InboundOrderId,
                        principalTable: "InboundOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Receipts_Locations_StagingLocationId",
                        column: x => x.StagingLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Receipts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    LabelType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ReadableText = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobItems_PrintJobs_PrintJobId",
                        column: x => x.PrintJobId,
                        principalTable: "PrintJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalInventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalInventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalInventories_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysicalInventories_StockSubjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "StockSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TxnGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceDocType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceDocNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockLedgers_TxnGroups_TxnGroupId",
                        column: x => x.TxnGroupId,
                        principalTable: "TxnGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UniqueCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniqueCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniqueCodes_InboundOrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalTable: "InboundOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderLineNo = table.Column<int>(type: "integer", nullable: true),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ActualQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QtyDiff = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceBatchNo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_InboundOrderLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalTable: "InboundOrderLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PutawayRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RecommendedLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceInventoryVersion = table.Column<int>(type: "integer", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PutawayAt = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PutawayRecords_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalTable: "ReceiptLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QualityChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExceptionReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PhotoIdsJson = table.Column<string>(type: "text", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    ResolutionAction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityChecks_ReceiptLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalTable: "ReceiptLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "RequiredPermissionCode",
                value: "action.receiving.create");

            migrationBuilder.InsertData(
                table: "MenuDefinitions",
                columns: new[] { "Id", "Code", "GroupKey", "IconKey", "ModuleCode", "ParentId", "Path", "RequiredPermissionCode", "Sort", "Surface", "TitleKey" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000006"), "pda.qc", null, null, "inbound", null, null, "action.quality.check", 20, "PDA", "pda.qc" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "pda.putaway", null, null, "inbound", null, null, "action.putaway.create", 30, "PDA", "pda.putaway" }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "Code", "ModuleCode", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000023"), "ACTION", "action.inbound-order.create", "inbound", "创建入库单" },
                    { new Guid("10000000-0000-0000-0000-000000000024"), "ACTION", "action.inbound-order.void", "inbound", "作废入库单" },
                    { new Guid("10000000-0000-0000-0000-000000000025"), "ACTION", "action.quality.check", "inbound", "PDA质检" },
                    { new Guid("10000000-0000-0000-0000-000000000026"), "ACTION", "action.quality.resolve", "inbound", "处理质检异常" },
                    { new Guid("10000000-0000-0000-0000-000000000027"), "ACTION", "action.putaway.create", "inbound", "PDA上架" },
                    { new Guid("10000000-0000-0000-0000-000000000028"), "ACTION", "action.attachment.upload", "inbound", "上传业务照片" },
                    { new Guid("10000000-0000-0000-0000-000000000029"), "ACTION", "action.print.create", "inbound", "生成固定模板打印" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000059"), new Guid("10000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000060"), new Guid("10000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000061"), new Guid("10000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000062"), new Guid("10000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000063"), new Guid("10000000-0000-0000-0000-000000000027"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000064"), new Guid("10000000-0000-0000-0000-000000000028"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000065"), new Guid("10000000-0000-0000-0000-000000000029"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000066"), new Guid("10000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0000-000000000067"), new Guid("10000000-0000-0000-0000-000000000027"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0000-000000000068"), new Guid("10000000-0000-0000-0000-000000000028"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0000-000000000069"), new Guid("10000000-0000-0000-0000-000000000029"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0000-000000000070"), new Guid("10000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000071"), new Guid("10000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000072"), new Guid("10000000-0000-0000-0000-000000000025"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000073"), new Guid("10000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000074"), new Guid("10000000-0000-0000-0000-000000000027"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000075"), new Guid("10000000-0000-0000-0000-000000000028"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000076"), new Guid("10000000-0000-0000-0000-000000000029"), new Guid("00000000-0000-0000-0000-000000000002") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_BizType_BizId",
                table: "Attachments",
                columns: new[] { "BizType", "BizId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_UploadedBy",
                table: "Attachments",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderLines_MaterialId",
                table: "InboundOrderLines",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderLines_OrderId_LineNo",
                table: "InboundOrderLines",
                columns: new[] { "OrderId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrders_OrderNo",
                table: "InboundOrders",
                column: "OrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrders_SourceType_SourceCode",
                table: "InboundOrders",
                columns: new[] { "SourceType", "SourceCode" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrders_WarehouseId_Status_CreatedAt",
                table: "InboundOrders",
                columns: new[] { "WarehouseId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventories_LocationId_SubjectId",
                table: "PhysicalInventories",
                columns: new[] { "LocationId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalInventories_SubjectId",
                table: "PhysicalInventories",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobItems_PrintJobId_Seq",
                table: "PrintJobItems",
                columns: new[] { "PrintJobId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_BizType_BizId_CreatedAt",
                table: "PrintJobs",
                columns: new[] { "BizType", "BizId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PutawayRecords_PutawayAt",
                table: "PutawayRecords",
                column: "PutawayAt");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayRecords_ReceiptLineId",
                table: "PutawayRecords",
                column: "ReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PutawayRecords_ToLocationId",
                table: "PutawayRecords",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_ReceiptLineId",
                table: "QualityChecks",
                column: "ReceiptLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityChecks_ResolutionAction_CheckedAt",
                table: "QualityChecks",
                columns: new[] { "ResolutionAction", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_BatchId",
                table: "ReceiptLines",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_MaterialId_BatchId_Status",
                table: "ReceiptLines",
                columns: new[] { "MaterialId", "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_OrderLineId",
                table: "ReceiptLines",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId_LineNo",
                table: "ReceiptLines",
                columns: new[] { "ReceiptId", "LineNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_InboundOrderId",
                table: "Receipts",
                column: "InboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ReceiptNo",
                table: "Receipts",
                column: "ReceiptNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_StagingLocationId",
                table: "Receipts",
                column: "StagingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_WarehouseId_Status_OccurredAt",
                table: "Receipts",
                columns: new[] { "WarehouseId", "Status", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgers_LocationId",
                table: "StockLedgers",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgers_OccurredAt",
                table: "StockLedgers",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgers_SubjectId",
                table: "StockLedgers",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StockLedgers_TxnGroupId_Seq",
                table: "StockLedgers",
                columns: new[] { "TxnGroupId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockSubjects_BatchId",
                table: "StockSubjects",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockSubjects_MaterialId",
                table: "StockSubjects",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_StockSubjects_WarehouseId_MaterialId_BatchId_Status",
                table: "StockSubjects",
                columns: new[] { "WarehouseId", "MaterialId", "BatchId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TxnGroups_GroupNo",
                table: "TxnGroups",
                column: "GroupNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueCodes_Code",
                table: "UniqueCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniqueCodes_OrderLineId",
                table: "UniqueCodes",
                column: "OrderLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "PhysicalInventories");

            migrationBuilder.DropTable(
                name: "PrintJobItems");

            migrationBuilder.DropTable(
                name: "PutawayRecords");

            migrationBuilder.DropTable(
                name: "QualityChecks");

            migrationBuilder.DropTable(
                name: "StockLedgers");

            migrationBuilder.DropTable(
                name: "UniqueCodes");

            migrationBuilder.DropTable(
                name: "StockSubjects");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "ReceiptLines");

            migrationBuilder.DropTable(
                name: "TxnGroups");

            migrationBuilder.DropTable(
                name: "InboundOrderLines");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "InboundOrders");

            migrationBuilder.DeleteData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000059"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000060"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000061"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000062"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000063"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000064"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000065"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000066"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000067"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000068"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000069"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000070"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000071"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000072"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000073"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000074"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000075"));

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000076"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000029"));

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IdempotencyRecords");

            migrationBuilder.UpdateData(
                table: "MenuDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "RequiredPermissionCode",
                value: "route.inbound");
        }
    }
}
