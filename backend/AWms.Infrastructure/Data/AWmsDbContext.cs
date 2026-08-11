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
            e.Property(x => x.CreatedAt).HasColumnType("timestamptz");
            e.Property(x => x.ExpiresAt).HasColumnType("timestamptz");
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
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000058"), RoleId = adminId, PermissionId = permActionSourceDelete }
        );

        // OPERATOR：仅入库（固定 GUID 3000…021+）
        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000021"), RoleId = operatorId, PermissionId = permRouteInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000022"), RoleId = operatorId, PermissionId = permMenuInbound },
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000023"), RoleId = operatorId, PermissionId = permActionReceivingCreate }
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
            new RolePermission { Id = Guid.Parse("30000000-0000-0000-0000-000000000049"), RoleId = supervisorId, PermissionId = permActionSourceDelete }
        );

        // Menus（固定 GUID 2000…）
        modelBuilder.Entity<MenuDefinition>().HasData(
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Code = "menu.dashboard", TitleKey = "nav.workspace", GroupKey = "nav.group.workspace", ModuleCode = "dashboard", IconKey = "home", Path = "/", Surface = Surface.WEB, Sort = 10 },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "menu.inbound", TitleKey = "nav.inbound", GroupKey = "nav.group.operations", ModuleCode = "inbound", IconKey = "inbox", Path = "/inbound", Surface = Surface.WEB, Sort = 20 },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Code = "menu.master-data", TitleKey = "nav.master-data", GroupKey = "nav.group.settings", ModuleCode = "master-data", IconKey = "database", Path = "/master-data", Surface = Surface.WEB, Sort = 30 },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Code = "menu.system", TitleKey = "nav.system", GroupKey = "nav.group.settings", ModuleCode = "system", IconKey = "settings", Path = "/system", Surface = Surface.WEB, Sort = 40 },
            new MenuDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Code = "pda.receiving", TitleKey = "pda.receiving", ModuleCode = "inbound", Sort = 10, Surface = Surface.PDA }
        );
    }
}
