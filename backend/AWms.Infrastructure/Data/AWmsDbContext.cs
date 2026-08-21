using Microsoft.EntityFrameworkCore;
using AWms.Domain.Entities;
using AWms.Domain.Enums;

namespace AWms.Infrastructure.Data;

public class AWmsDbContext : DbContext
{
    public AWmsDbContext(DbContextOptions<AWmsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<MenuDefinition> MenuDefinitions => Set<MenuDefinition>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Sequence> Sequences => Set<Sequence>();
    public DbSet<ImportTask> ImportTasks => Set<ImportTask>();
    public DbSet<ImportTaskDetail> ImportTaskDetails => Set<ImportTaskDetail>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<InboundOrder> InboundOrders => Set<InboundOrder>();
    public DbSet<InboundOrderLine> InboundOrderLines => Set<InboundOrderLine>();
    public DbSet<UniqueCode> UniqueCodes => Set<UniqueCode>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<QualityCheck> QualityChecks => Set<QualityCheck>();
    public DbSet<PutawayRecord> PutawayRecords => Set<PutawayRecord>();
    public DbSet<StockSubject> StockSubjects => Set<StockSubject>();
    public DbSet<PhysicalInventory> PhysicalInventories => Set<PhysicalInventory>();
    public DbSet<TxnGroup> TxnGroups => Set<TxnGroup>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<PrintJobItem> PrintJobItems => Set<PrintJobItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- User ---
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        });

        // --- Role ---
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        // --- Permission ---
        modelBuilder.Entity<Permission>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(128).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ModuleCode).HasMaxLength(64).IsRequired();
        });

        // --- UserRole ---
        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            e.HasIndex(x => x.RoleId);
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        // --- RolePermission ---
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
            e.HasIndex(x => x.PermissionId);
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
        });

        // --- MenuDefinition ---
        modelBuilder.Entity<MenuDefinition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ParentId);
            e.Property(x => x.Code).HasMaxLength(128).IsRequired();
            e.Property(x => x.TitleKey).HasMaxLength(128);
            e.Property(x => x.Surface).HasConversion<string>().HasMaxLength(10);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
        });

        // --- Material ---
        modelBuilder.Entity<Material>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.SearchCode);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.SearchCode).HasMaxLength(32);
            e.Property(x => x.LabelType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DefaultUom).HasMaxLength(10);
            e.Property(x => x.DefaultQtyPerLabel).HasColumnType("decimal(18,4)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        });

        // --- Warehouse ---
        modelBuilder.Entity<Warehouse>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.SearchCode);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.SearchCode).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MgmtMode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        });

        // --- Location ---
        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
            e.HasIndex(x => x.SearchCode);
            e.HasIndex(x => x.WarehouseId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.SearchCode).HasMaxLength(32);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reachability).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            e.HasOne(x => x.Warehouse).WithMany(x => x.Locations).HasForeignKey(x => x.WarehouseId);
        });

        // --- Source ---
        modelBuilder.Entity<Source>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Type, x.Code }).IsUnique();
            e.HasIndex(x => x.SearchCode);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.SearchCode).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        });

        // --- Batch ---
        modelBuilder.Entity<Batch>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MaterialId, x.BatchNo }).IsUnique();
            e.HasIndex(x => x.MaterialId);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Status);
            e.Property(x => x.MaterialCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.BatchNo).HasMaxLength(32).IsRequired();
            e.Property(x => x.SourceBatchNo).HasMaxLength(128);
            e.Property(x => x.SourceType).HasMaxLength(20);
            e.Property(x => x.SourceCode).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
        });

        // --- Sequence ---
        modelBuilder.Entity<Sequence>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Type, x.ScopeKey, x.BizDate }).IsUnique();
            e.Property(x => x.Type).HasMaxLength(64).IsRequired();
            e.Property(x => x.ScopeKey).HasMaxLength(64).IsRequired();
        });

        // --- ImportTask ---
        modelBuilder.Entity<ImportTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TaskNo).IsUnique();
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.OperatorId);
            e.Property(x => x.TaskNo).HasMaxLength(64).IsRequired();
            e.Property(x => x.ModuleCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(256);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.CompletedAt).HasColumnType("timestamptz");
        });

        // --- ImportTaskDetail ---
        modelBuilder.Entity<ImportTaskDetail>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ImportTaskId);
            e.HasOne(x => x.ImportTask).WithMany(x => x.Details).HasForeignKey(x => x.ImportTaskId);
        });

        // --- IdempotencyRecord ---
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.Property(x => x.Key).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(IdempotencyStatus.PENDING);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.ExpiresAt).HasColumnType("timestamptz");
        });

        // --- InboundOrder ---
        modelBuilder.Entity<InboundOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.HasIndex(x => new { x.WarehouseId, x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.SourceType, x.SourceCode });
            e.Property(x => x.OrderNo).HasMaxLength(64).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceCode).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedBy).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
            e.Property(x => x.VoidedAt).HasColumnType("timestamptz");
            e.Property(x => x.VoidedBy).HasMaxLength(128);
            e.Property(x => x.VoidReason).HasMaxLength(512);
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
        });

        // --- InboundOrderLine ---
        modelBuilder.Entity<InboundOrderLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrderId, x.LineNo }).IsUnique();
            e.HasIndex(x => x.MaterialId);
            e.Property(x => x.ExpectedQty).HasColumnType("decimal(18,4)");
            e.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
        });

        // --- UniqueCode ---
        modelBuilder.Entity<UniqueCode>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.OrderLineId);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ReceivedAt).HasColumnType("timestamptz");
            e.HasOne(x => x.OrderLine).WithMany(x => x.UniqueCodes).HasForeignKey(x => x.OrderLineId);
        });

        // --- Receipt ---
        modelBuilder.Entity<Receipt>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReceiptNo).IsUnique();
            e.HasIndex(x => new { x.WarehouseId, x.Status, x.OccurredAt });
            e.HasIndex(x => x.InboundOrderId);
            e.Property(x => x.ReceiptNo).HasMaxLength(64).IsRequired();
            e.Property(x => x.SourceDocType).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.SourceDocNo).HasMaxLength(64);
            e.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceCode).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OperatorName).HasMaxLength(128).IsRequired();
            e.Property(x => x.OccurredAt).HasColumnType("timestamptz");
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
            e.HasOne(x => x.StagingLocation).WithMany().HasForeignKey(x => x.StagingLocationId);
            e.HasOne(x => x.InboundOrder).WithMany().HasForeignKey(x => x.InboundOrderId);
        });

        // --- ReceiptLine ---
        modelBuilder.Entity<ReceiptLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ReceiptId, x.LineNo }).IsUnique();
            e.HasIndex(x => new { x.ReceiptId, x.OrderLineId })
                .IsUnique()
                .HasFilter("\"OrderLineId\" IS NOT NULL");
            e.HasIndex(x => x.OrderLineId);
            e.HasIndex(x => new { x.MaterialId, x.BatchId, x.Status });
            e.Property(x => x.ExpectedQty).HasColumnType("decimal(18,4)");
            e.Property(x => x.ActualQty).HasColumnType("decimal(18,4)");
            e.Property(x => x.QtyDiff).HasColumnType("decimal(18,4)");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceBatchNo).HasMaxLength(128);
            e.Property(x => x.ReceivedAt).HasColumnType("timestamptz");
            e.HasOne(x => x.Receipt).WithMany(x => x.Lines).HasForeignKey(x => x.ReceiptId);
            e.HasOne(x => x.OrderLine).WithMany().HasForeignKey(x => x.OrderLineId);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId);
        });

        // --- QualityCheck ---
        modelBuilder.Entity<QualityCheck>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReceiptLineId).IsUnique();
            e.HasIndex(x => new { x.ResolutionAction, x.CheckedAt });
            e.Property(x => x.CheckedQty).HasColumnType("decimal(18,4)");
            e.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ExceptionReason).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(512);
            e.Property(x => x.PhotoIdsJson).IsRequired();
            e.Property(x => x.OperatorName).HasMaxLength(128).IsRequired();
            e.Property(x => x.CheckedAt).HasColumnType("timestamptz");
            e.Property(x => x.ResolutionAction).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ResolutionNote).HasMaxLength(512);
            e.Property(x => x.ResolvedByName).HasMaxLength(128);
            e.Property(x => x.ResolvedAt).HasColumnType("timestamptz");
            e.HasOne(x => x.ReceiptLine).WithOne().HasForeignKey<QualityCheck>(x => x.ReceiptLineId);
        });

        // --- PutawayRecord ---
        modelBuilder.Entity<PutawayRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReceiptLineId).IsUnique();
            e.HasIndex(x => x.ToLocationId);
            e.HasIndex(x => x.PutawayAt);
            e.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            e.Property(x => x.OperatorName).HasMaxLength(128).IsRequired();
            e.Property(x => x.PutawayAt).HasColumnType("timestamptz");
            e.HasOne(x => x.ReceiptLine).WithOne().HasForeignKey<PutawayRecord>(x => x.ReceiptLineId);
        });

        // --- StockSubject ---
        modelBuilder.Entity<StockSubject>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WarehouseId, x.MaterialId, x.BatchId, x.Status }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Uom).HasMaxLength(10).IsRequired();
            e.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId);
            e.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId);
        });

        // --- PhysicalInventory ---
        modelBuilder.Entity<PhysicalInventory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LocationId, x.SubjectId }).IsUnique();
            e.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId);
        });

        // --- TxnGroup ---
        modelBuilder.Entity<TxnGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GroupNo).IsUnique();
            e.Property(x => x.GroupNo).HasMaxLength(64).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        });

        // --- StockLedger ---
        modelBuilder.Entity<StockLedger>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TxnGroupId, x.Seq }).IsUnique();
            e.HasIndex(x => x.SubjectId);
            e.HasIndex(x => x.LocationId);
            e.HasIndex(x => x.OccurredAt);
            e.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            e.Property(x => x.BalanceBefore).HasColumnType("decimal(18,4)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(18,4)");
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.SourceDocType).HasMaxLength(64);
            e.Property(x => x.SourceDocNo).HasMaxLength(64);
            e.Property(x => x.OccurredAt).HasColumnType("timestamptz");
            e.HasOne(x => x.TxnGroup).WithMany().HasForeignKey(x => x.TxnGroupId);
        });

        // --- Attachment ---
        modelBuilder.Entity<Attachment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BizType, x.BizId });
            e.HasIndex(x => x.UploadedBy);
            e.Property(x => x.FileName).HasMaxLength(256).IsRequired();
            e.Property(x => x.MimeType).HasMaxLength(80).IsRequired();
            e.Property(x => x.Path).HasMaxLength(512).IsRequired();
            e.Property(x => x.BizType).HasMaxLength(30);
            e.Property(x => x.UploadedByName).HasMaxLength(128).IsRequired();
            e.Property(x => x.UploadedAt).HasColumnType("timestamptz");
        });

        // --- PrintJob ---
        modelBuilder.Entity<PrintJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BizType, x.BizId, x.CreatedAt });
            e.Property(x => x.BizType).HasMaxLength(30);
            e.Property(x => x.TemplateCode).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FilePath).HasMaxLength(512);
            e.Property(x => x.ErrorCode).HasMaxLength(64);
            e.Property(x => x.ErrorMessage).HasMaxLength(512);
            e.Property(x => x.CreatedByName).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        });

        // --- PrintJobItem ---
        modelBuilder.Entity<PrintJobItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PrintJobId, x.Seq }).IsUnique();
            e.Property(x => x.LabelType).HasMaxLength(10).IsRequired();
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.ReadableText).IsRequired();
            e.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
            e.HasOne(x => x.PrintJob).WithMany(x => x.Items).HasForeignKey(x => x.PrintJobId);
        });

        // === Seed data ===
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // 固定 GUID 常量：角色 / 权限 / 角色-权限 / 菜单（禁止 Guid.NewGuid()，规范 §3.1、复验意见）
        var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var supervisorId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var operatorId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        var permRouteInbound = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var permMenuInbound = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var permActionReceivingCreate = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var permActionInboundOrderCreate = Guid.Parse("10000000-0000-0000-0000-000000000023");
        var permActionInboundOrderVoid = Guid.Parse("10000000-0000-0000-0000-000000000024");
        var permActionQualityCheck = Guid.Parse("10000000-0000-0000-0000-000000000025");
        var permActionQualityResolve = Guid.Parse("10000000-0000-0000-0000-000000000026");
        var permActionPutawayCreate = Guid.Parse("10000000-0000-0000-0000-000000000027");
        var permActionAttachmentUpload = Guid.Parse("10000000-0000-0000-0000-000000000028");
        var permActionPrintCreate = Guid.Parse("10000000-0000-0000-0000-000000000029");
        var permRouteMasterData = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var permMenuMasterData = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var permActionMaterialCreate = Guid.Parse("10000000-0000-0000-0000-000000000006");
        var permActionMaterialEdit = Guid.Parse("10000000-0000-0000-0000-000000000007");
        var permActionMaterialDelete = Guid.Parse("10000000-0000-0000-0000-000000000008");
        var permActionImport = Guid.Parse("10000000-0000-0000-0000-000000000009");
        var permActionExport = Guid.Parse("10000000-0000-0000-0000-000000000010");
        var permRouteSystem = Guid.Parse("10000000-0000-0000-0000-000000000011");
        var permMenuSystem = Guid.Parse("10000000-0000-0000-0000-000000000012");
        var permActionUserManage = Guid.Parse("10000000-0000-0000-0000-000000000013");

        var permActionWarehouseCreate = Guid.Parse("10000000-0000-0000-0000-000000000014");
        var permActionWarehouseEdit = Guid.Parse("10000000-0000-0000-0000-000000000015");
        var permActionWarehouseDelete = Guid.Parse("10000000-0000-0000-0000-000000000016");
        var permActionLocationCreate = Guid.Parse("10000000-0000-0000-0000-000000000017");
        var permActionLocationEdit = Guid.Parse("10000000-0000-0000-0000-000000000018");
        var permActionLocationDelete = Guid.Parse("10000000-0000-0000-0000-000000000019");
        var permActionSourceCreate = Guid.Parse("10000000-0000-0000-0000-000000000020");
        var permActionSourceEdit = Guid.Parse("10000000-0000-0000-0000-000000000021");
        var permActionSourceDelete = Guid.Parse("10000000-0000-0000-0000-000000000022");

        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = permRouteInbound, Code = "route.inbound", Name = "入库模块", Category = PermissionCategory.ROUTE, ModuleCode = "inbound" },
            new Permission { Id = permMenuInbound, Code = "menu.inbound", Name = "入库菜单", Category = PermissionCategory.MENU, ModuleCode = "inbound" },
            new Permission { Id = permActionReceivingCreate, Code = "action.receiving.create", Name = "创建收货", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionInboundOrderCreate, Code = "action.inbound-order.create", Name = "创建入库单", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionInboundOrderVoid, Code = "action.inbound-order.void", Name = "作废入库单", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionQualityCheck, Code = "action.quality.check", Name = "PDA质检", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionQualityResolve, Code = "action.quality.resolve", Name = "处理质检异常", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionPutawayCreate, Code = "action.putaway.create", Name = "PDA上架", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionAttachmentUpload, Code = "action.attachment.upload", Name = "上传业务照片", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permActionPrintCreate, Code = "action.print.create", Name = "生成固定模板打印", Category = PermissionCategory.ACTION, ModuleCode = "inbound" },
            new Permission { Id = permRouteMasterData, Code = "route.master-data", Name = "主数据模块", Category = PermissionCategory.ROUTE, ModuleCode = "master-data" },
            new Permission { Id = permMenuMasterData, Code = "menu.master-data", Name = "主数据菜单", Category = PermissionCategory.MENU, ModuleCode = "master-data" },
            new Permission { Id = permActionMaterialCreate, Code = "action.material.create", Name = "创建物料", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionMaterialEdit, Code = "action.material.edit", Name = "编辑物料", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionMaterialDelete, Code = "action.material.delete", Name = "删除物料", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionImport, Code = "action.import", Name = "导入", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionExport, Code = "action.export", Name = "导出", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permRouteSystem, Code = "route.system", Name = "系统模块", Category = PermissionCategory.ROUTE, ModuleCode = "system" },
            new Permission { Id = permMenuSystem, Code = "menu.system", Name = "系统菜单", Category = PermissionCategory.MENU, ModuleCode = "system" },
            new Permission { Id = permActionUserManage, Code = "action.user.manage", Name = "用户管理", Category = PermissionCategory.ACTION, ModuleCode = "system" },
            new Permission { Id = permActionWarehouseCreate, Code = "action.warehouse.create", Name = "创建仓库", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionWarehouseEdit, Code = "action.warehouse.edit", Name = "编辑仓库", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionWarehouseDelete, Code = "action.warehouse.delete", Name = "删除仓库", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionLocationCreate, Code = "action.location.create", Name = "创建库位", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionLocationEdit, Code = "action.location.edit", Name = "编辑库位", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionLocationDelete, Code = "action.location.delete", Name = "删除库位", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionSourceCreate, Code = "action.source.create", Name = "创建来源", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionSourceEdit, Code = "action.source.edit", Name = "编辑来源", Category = PermissionCategory.ACTION, ModuleCode = "master-data" },
            new Permission { Id = permActionSourceDelete, Code = "action.source.delete", Name = "删除来源", Category = PermissionCategory.ACTION, ModuleCode = "master-data" }
        );

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = adminId, Code = "SYSTEM_ADMIN", Name = "系统管理员", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = supervisorId, Code = "SUPERVISOR", Name = "仓管", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = operatorId, Code = "OPERATOR", Name = "作业员", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // SYSTEM_ADMIN：全部权限（固定 GUID 3000…）
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), RoleId = adminId, PermissionId = permRouteInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), RoleId = adminId, PermissionId = permMenuInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000003"), RoleId = adminId, PermissionId = permActionReceivingCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000004"), RoleId = adminId, PermissionId = permRouteMasterData },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000005"), RoleId = adminId, PermissionId = permMenuMasterData },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000006"), RoleId = adminId, PermissionId = permActionMaterialCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000007"), RoleId = adminId, PermissionId = permActionMaterialEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000008"), RoleId = adminId, PermissionId = permActionMaterialDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000009"), RoleId = adminId, PermissionId = permActionImport },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000010"), RoleId = adminId, PermissionId = permActionExport },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000011"), RoleId = adminId, PermissionId = permRouteSystem },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000012"), RoleId = adminId, PermissionId = permMenuSystem },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000013"), RoleId = adminId, PermissionId = permActionUserManage },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000050"), RoleId = adminId, PermissionId = permActionWarehouseCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000051"), RoleId = adminId, PermissionId = permActionWarehouseEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000052"), RoleId = adminId, PermissionId = permActionWarehouseDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000053"), RoleId = adminId, PermissionId = permActionLocationCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000054"), RoleId = adminId, PermissionId = permActionLocationEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000055"), RoleId = adminId, PermissionId = permActionLocationDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000056"), RoleId = adminId, PermissionId = permActionSourceCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000057"), RoleId = adminId, PermissionId = permActionSourceEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000058"), RoleId = adminId, PermissionId = permActionSourceDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000059"), RoleId = adminId, PermissionId = permActionInboundOrderCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000060"), RoleId = adminId, PermissionId = permActionInboundOrderVoid },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000061"), RoleId = adminId, PermissionId = permActionQualityCheck },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000062"), RoleId = adminId, PermissionId = permActionQualityResolve },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000063"), RoleId = adminId, PermissionId = permActionPutawayCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000064"), RoleId = adminId, PermissionId = permActionAttachmentUpload },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000065"), RoleId = adminId, PermissionId = permActionPrintCreate }
        );

        // OPERATOR：仅入库（固定 GUID 3000…021+）
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000021"), RoleId = operatorId, PermissionId = permRouteInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000022"), RoleId = operatorId, PermissionId = permMenuInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000023"), RoleId = operatorId, PermissionId = permActionReceivingCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000066"), RoleId = operatorId, PermissionId = permActionQualityCheck },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000067"), RoleId = operatorId, PermissionId = permActionPutawayCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000068"), RoleId = operatorId, PermissionId = permActionAttachmentUpload },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000069"), RoleId = operatorId, PermissionId = permActionPrintCreate }
        );

        // SUPERVISOR：入库 + 主数据（固定 GUID 3000…031+）
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000031"), RoleId = supervisorId, PermissionId = permRouteInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000032"), RoleId = supervisorId, PermissionId = permMenuInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000033"), RoleId = supervisorId, PermissionId = permActionReceivingCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000034"), RoleId = supervisorId, PermissionId = permRouteMasterData },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000035"), RoleId = supervisorId, PermissionId = permMenuMasterData },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000036"), RoleId = supervisorId, PermissionId = permActionMaterialCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000037"), RoleId = supervisorId, PermissionId = permActionMaterialEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000038"), RoleId = supervisorId, PermissionId = permActionMaterialDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000039"), RoleId = supervisorId, PermissionId = permActionImport },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000040"), RoleId = supervisorId, PermissionId = permActionExport },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000041"), RoleId = supervisorId, PermissionId = permActionWarehouseCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000042"), RoleId = supervisorId, PermissionId = permActionWarehouseEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000043"), RoleId = supervisorId, PermissionId = permActionWarehouseDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000044"), RoleId = supervisorId, PermissionId = permActionLocationCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000045"), RoleId = supervisorId, PermissionId = permActionLocationEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000046"), RoleId = supervisorId, PermissionId = permActionLocationDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000047"), RoleId = supervisorId, PermissionId = permActionSourceCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000048"), RoleId = supervisorId, PermissionId = permActionSourceEdit },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000049"), RoleId = supervisorId, PermissionId = permActionSourceDelete },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000070"), RoleId = supervisorId, PermissionId = permActionInboundOrderCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000071"), RoleId = supervisorId, PermissionId = permActionInboundOrderVoid },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000072"), RoleId = supervisorId, PermissionId = permActionQualityCheck },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000073"), RoleId = supervisorId, PermissionId = permActionQualityResolve },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000074"), RoleId = supervisorId, PermissionId = permActionPutawayCreate },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000075"), RoleId = supervisorId, PermissionId = permActionAttachmentUpload },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000076"), RoleId = supervisorId, PermissionId = permActionPrintCreate }
        );

        // Menus（固定 GUID 2000…）
        modelBuilder.Entity<MenuDefinition>().HasData(
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Code = "menu.dashboard", TitleKey = "nav.workspace", GroupKey = "nav.group.workspace", ModuleCode = "dashboard", IconKey = "home", Path = "/", Surface = Surface.WEB, Sort = 10 },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "menu.inbound", TitleKey = "nav.inbound", GroupKey = "nav.group.operations", ModuleCode = "inbound", IconKey = "inbox", Path = "/inbound", Surface = Surface.WEB, Sort = 20, RequiredPermissionCode = "route.inbound" },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Code = "menu.master-data", TitleKey = "nav.master-data", GroupKey = "nav.group.settings", ModuleCode = "master-data", IconKey = "database", Path = "/master-data", Surface = Surface.WEB, Sort = 30, RequiredPermissionCode = "route.master-data" },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Code = "menu.system", TitleKey = "nav.system", GroupKey = "nav.group.settings", ModuleCode = "system", IconKey = "settings", Path = "/system", Surface = Surface.WEB, Sort = 40, RequiredPermissionCode = "route.system" },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Code = "pda.receiving", TitleKey = "pda.receiving", ModuleCode = "inbound", Sort = 10, Surface = Surface.PDA, RequiredPermissionCode = "action.receiving.create" },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), Code = "pda.qc", TitleKey = "pda.qc", ModuleCode = "inbound", Sort = 20, Surface = Surface.PDA, RequiredPermissionCode = "action.quality.check" },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), Code = "pda.putaway", TitleKey = "pda.putaway", ModuleCode = "inbound", Sort = 30, Surface = Surface.PDA, RequiredPermissionCode = "action.putaway.create" }
        );
    }
}
