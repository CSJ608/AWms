using AWms.Domain.Dtos.Materials;
using AWms.Domain.Dtos.Sources;
using AWms.Domain.Dtos.Warehouses;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.Tests.Services;

public class MasterDataServiceTests
{
    private AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    [Fact]
    public async Task CreateMaterialAsync_DuplicateCode_Throws409()
    {
        var db = CreateDb();
        db.Materials.Add(new Material { Code = "MAT-001", Name = "物料1", DefaultUom = "CT" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateMaterialAsync(new("MAT-001", "重复", null, false, "NONE", "CT", null, null)));
        Assert.Equal("MATERIAL_CODE_DUPLICATED", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateMaterialAsync_ValidData_ReturnsMaterialItem()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var result = await service.CreateMaterialAsync(new("MAT-002", "螺母 M6", "LM", true, "SKU", "CT", "10.0000", "ENABLED"));

        Assert.Equal("MAT-002", result.Code);
        Assert.Equal("螺母 M6", result.Name);
        Assert.Equal("LM", result.SearchCode);
        Assert.True(result.BatchControlled);
        Assert.Equal("SKU", result.LabelType);
        Assert.Equal("10.0000", result.DefaultQtyPerLabel);
    }

    [Fact]
    public async Task DeleteMaterialAsync_ReferencedByBatch_Throws409()
    {
        var db = CreateDb();
        var mat = new Material { Code = "MAT-001", Name = "物料1" };
        db.Materials.Add(mat);
        db.Batches.Add(new Batch { MaterialId = mat.Id, MaterialCode = "MAT-001", BatchNo = "260810001" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteMaterialAsync(mat.Id));
        Assert.Equal("MATERIAL_IN_USE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task SearchMaterialsAsync_KeywordMatchesCodeNameSearchCode()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-001", Name = "Alpha", SearchCode = "A" },
            new Material { Code = "MAT-002", Name = "Beta", SearchCode = "B" },
            new Material { Code = "MAT-003", Name = "Gamma", SearchCode = "GA" }
        );
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var result = await service.SearchMaterialsAsync(new FilterRequest("A", null, null, null, null, null, null, null, null, null, 1, 20));
        Assert.Equal(3, result.Total); // MAT-001 Alpha, MAT-003 Gamma, and MAT-001 code contains A

        result = await service.SearchMaterialsAsync(new FilterRequest("B", null, null, null, null, null, null, null, null, null, 1, 20));
        Assert.Equal(1, result.Total); // Only Beta (name contains B); "B" searchCode is for MAT-002 but B != b after ToLower
    }

    [Fact]
    public async Task CreateWarehouseAsync_DuplicateCode_Throws409()
    {
        var db = CreateDb();
        db.Warehouses.Add(new Warehouse { Code = "WH-01", Name = "仓1" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateWarehouseAsync(new("WH-01", "重复", null, null, null)));
        Assert.Equal("WAREHOUSE_CODE_DUPLICATED", ex.Code);
    }

    [Fact]
    public async Task CreateSourceAsync_DuplicateTypeAndCode_Throws409()
    {
        var db = CreateDb();
        db.Sources.Add(new Source { Type = SourceType.SUPPLIER, Code = "SUP-001", Name = "供应商1" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSourceAsync(new("SUPPLIER", "SUP-001", "重复", null, null)));
        Assert.Equal("SOURCE_CODE_DUPLICATED", ex.Code);
    }

    [Fact]
    public async Task SearchBatchesAsync_DefaultOrderIsCreatedAtDesc()
    {
        var db = CreateDb();
        var mat = new Material { Code = "MAT-001", Name = "物料1" };
        db.Materials.Add(mat);
        db.Batches.AddRange(
            new Batch { MaterialId = mat.Id, MaterialCode = "MAT-001", BatchNo = "260810001", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Batch { MaterialId = mat.Id, MaterialCode = "MAT-001", BatchNo = "260810002", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var result = await service.SearchBatchesAsync(new FilterRequest(null, null, null, null, null, null, null, null, null, null, 1, 20));
        Assert.Equal(2, result.Total);
        // Latest first (createdAt DESC default)
        Assert.Equal("260810002", result.Items[0].BatchNo);
        Assert.Equal("260810001", result.Items[1].BatchNo);
    }

    [Fact]
    public async Task CreateLocationAsync_DuplicateCodeInWarehouse_Throws409()
    {
        var db = CreateDb();
        var wh = new Warehouse { Code = "WH-01", Name = "仓1" };
        db.Warehouses.Add(wh);
        db.Locations.Add(new Location { WarehouseId = wh.Id, Code = "LOC-01" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateLocationAsync(wh.Id, new("LOC-01", null, "DEFAULT", null)));
        Assert.Equal("LOCATION_CODE_DUPLICATED", ex.Code);
    }

    [Fact]
    public async Task DeleteWarehouse_WithLocations_Throws409()
    {
        var db = CreateDb();
        var wh = new Warehouse { Code = "WH-01", Name = "仓1" };
        wh.Locations.Add(new Location { Code = "LOC-01" });
        db.Warehouses.Add(wh);
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteWarehouseAsync(wh.Id));
        Assert.Equal("WAREHOUSE_IN_USE", ex.Code);
    }

    [Fact]
    public async Task SearchMaterialsAsync_SortByName_RespectsUserSort()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-003", Name = "Zulu" },
            new Material { Code = "MAT-001", Name = "Alpha" },
            new Material { Code = "MAT-002", Name = "Beta" }
        );
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var result = await service.SearchMaterialsAsync(new FilterRequest(null, null, null, null, null, null, null, null,
            new List<SortOption> { new("name", "asc") }, null, 1, 20));

        Assert.Equal(3, result.Total);
        Assert.Equal("Alpha", result.Items[0].Name);
        Assert.Equal("Beta", result.Items[1].Name);
        Assert.Equal("Zulu", result.Items[2].Name);
    }

    [Fact]
    public async Task SearchMaterialsAsync_SortByNameDesc_RespectsUserSort()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-003", Name = "Zulu" },
            new Material { Code = "MAT-001", Name = "Alpha" },
            new Material { Code = "MAT-002", Name = "Beta" }
        );
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var result = await service.SearchMaterialsAsync(new FilterRequest(null, null, null, null, null, null, null, null,
            new List<SortOption> { new("name", "desc") }, null, 1, 20));

        Assert.Equal(3, result.Total);
        Assert.Equal("Zulu", result.Items[0].Name);
        Assert.Equal("Beta", result.Items[1].Name);
        Assert.Equal("Alpha", result.Items[2].Name);
    }

    [Fact]
    public async Task SearchMaterialsAsync_InvalidSortField_Throws400()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.SearchMaterialsAsync(new FilterRequest(null, null, null, null, null, null, null, null,
                new List<SortOption> { new("invalidField", "asc") }, null, 1, 20)));

        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Fact]
    public async Task CreateLocationAsync_仓内重复码_抛409()
    {
        var db = CreateDb();
        var wh = new Warehouse { Code = "WH-01", Name = "仓1" };
        db.Warehouses.Add(wh);
        db.Locations.Add(new Location { WarehouseId = wh.Id, Code = "STG-01", Type = LocationType.STAGING });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateLocationAsync(wh.Id, new("STG-01", null, "STAGING", null)));
        Assert.Equal("LOCATION_CODE_DUPLICATED", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateLocationAsync_仓库不存在_抛404()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateLocationAsync(Guid.NewGuid(), new("STG-01", null, "STAGING", null)));
        Assert.Equal("WAREHOUSE_NOT_FOUND", ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CreateLocationAsync_库位类型无效_抛400()
    {
        var db = CreateDb();
        var wh = new Warehouse { Code = "WH-01", Name = "仓1" };
        db.Warehouses.Add(wh);
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateLocationAsync(wh.Id, new("STG-01", null, "BOGUS", null)));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteWarehouseAsync_有库位_抛409()
    {
        var db = CreateDb();
        var wh = new Warehouse { Code = "WH-01", Name = "仓1" };
        db.Warehouses.Add(wh);
        db.Locations.Add(new Location { WarehouseId = wh.Id, Code = "STG-01", Type = LocationType.STAGING });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteWarehouseAsync(wh.Id));
        Assert.Equal("WAREHOUSE_IN_USE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteSourceAsync_被批次引用_抛409()
    {
        var db = CreateDb();
        var src = new Source { Type = SourceType.SUPPLIER, Code = "SUP-001", Name = "供应商1" };
        var mat = new Material { Code = "MAT-001", Name = "物料1" };
        db.Sources.Add(src);
        db.Materials.Add(mat);
        db.Batches.Add(new Batch { MaterialId = mat.Id, MaterialCode = "MAT-001", BatchNo = "260810001", SourceType = "SUPPLIER", SourceCode = "SUP-001" });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.DeleteSourceAsync(src.Id));
        Assert.Equal("SOURCE_IN_USE", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateMaterialAsync_非法枚举_抛400()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateMaterialAsync(new("MAT-001", "物料1", null, false, "NOPE", "CT", null, "ENABLED")));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateMaterialAsync_非法UOM_抛400()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateMaterialAsync(new("MAT-001", "物料1", null, false, "NONE", "XX", null, "ENABLED")));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SearchMaterialBatchesAsync_物料不存在_抛404()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.SearchMaterialBatchesAsync(Guid.NewGuid(), new FilterRequest(null, null, null, null, null, null, null, null, null, null, 1, 20)));
        Assert.Equal("MATERIAL_NOT_FOUND", ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task SearchMaterialsAsync_带filter_真实筛选()
    {
        var db = CreateDb();
        db.Materials.AddRange(
            new Material { Code = "MAT-001", Status = MaterialStatus.ENABLED },
            new Material { Code = "MAT-002", Status = MaterialStatus.DISABLED });
        await db.SaveChangesAsync();
        var service = new MasterDataService(db, new QueryService());
        var filter = new FilterGroup("and", new List<FilterCondition>
        {
            new("status", "eq", "ENABLED")
        });

        var result = await service.SearchMaterialsAsync(new FilterRequest(null, null, null, null, null, null, null, null, null, filter, 1, 20));

        Assert.Equal(1, result.Total);
        Assert.Equal("MAT-001", result.Items[0].Code);
    }

    [Fact]
    public async Task SearchMaterialsAsync_白名单外字段_抛400()
    {
        var db = CreateDb();
        var service = new MasterDataService(db, new QueryService());
        var filter = new FilterGroup("and", new List<FilterCondition>
        {
            new("hackedField", "eq", "x")
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.SearchMaterialsAsync(new FilterRequest(null, null, null, null, null, null, null, null, null, filter, 1, 20)));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }
}
