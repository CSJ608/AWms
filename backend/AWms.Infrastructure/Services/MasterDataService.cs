using System.Globalization;
using AWms.Domain.Dtos.Batches;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.Materials;
using AWms.Domain.Dtos.Sources;
using AWms.Domain.Dtos.Warehouses;
using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AWms.Infrastructure.Services;

public class MasterDataService
{
    private readonly AWmsDbContext _db;
    private readonly IQueryService _queryService;

    public MasterDataService(AWmsDbContext db, IQueryService queryService)
    {
        _db = db;
        _queryService = queryService;
    }

    // === 字段/排序白名单（契约 2.10 + 各资源契约）===
    private static readonly HashSet<string> MaterialFields = new(StringComparer.Ordinal) { "code", "name", "searchCode", "batchControlled", "labelType", "defaultUom", "defaultQtyPerLabel", "status", "createdAt", "updatedAt" };
    private static readonly HashSet<string> MaterialSorts = new(StringComparer.Ordinal) { "code", "name", "status", "updatedAt" };
    private static readonly HashSet<string> WarehouseFields = new(StringComparer.Ordinal) { "code", "name", "searchCode", "status", "mgmtMode", "createdAt" };
    private static readonly HashSet<string> WarehouseSorts = new(StringComparer.Ordinal) { "code", "name", "status", "createdAt" };
    private static readonly HashSet<string> LocationFields = new(StringComparer.Ordinal) { "code", "searchCode", "type", "status", "reachability", "createdAt" };
    private static readonly HashSet<string> LocationSorts = new(StringComparer.Ordinal) { "code", "type", "status", "createdAt" };
    private static readonly HashSet<string> SourceFields = new(StringComparer.Ordinal) { "type", "code", "name", "searchCode", "status", "createdAt" };
    private static readonly HashSet<string> SourceSorts = new(StringComparer.Ordinal) { "code", "name", "status" };
    private static readonly HashSet<string> BatchFields = new(StringComparer.Ordinal) { "materialId", "materialCode", "batchNo", "sourceBatchNo", "sourceType", "sourceCode", "productionDate", "expiryDate", "status", "createdAt" };
    private static readonly HashSet<string> BatchSorts = new(StringComparer.Ordinal) { "batchNo", "materialCode", "status", "createdAt" };

    // === Materials ===
    public async Task<PagedResult<MaterialItem>> SearchMaterialsAsync(FilterRequest request)
    {
        var query = _db.Materials.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLowerInvariant();
            query = query.Where(m =>
                m.Code.ToLower().Contains(kw) ||
                m.Name.ToLower().Contains(kw) ||
                (m.SearchCode != null && m.SearchCode.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrWhiteSpace(request.Code))
            query = query.Where(m => m.Code.ToLower().Contains(request.Code.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(request.Name))
            query = query.Where(m => m.Name.ToLower().Contains(request.Name.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(m => m.Status == ParseEnum<MaterialStatus>(request.Status, "status"));
        if (!string.IsNullOrWhiteSpace(request.LabelType))
            query = query.Where(m => m.LabelType == ParseEnum<LabelType>(request.LabelType, "labelType"));

        var (_, result) = await _queryService.ApplyAsync(query, request, MaterialFields, MaterialSorts, "code", "asc");
        return new PagedResult<MaterialItem>(result.Items.Select(MapMaterial).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<MaterialItem?> GetMaterialAsync(Guid id)
    {
        var m = await _db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return m == null ? null : MapMaterial(m);
    }

    public async Task<MaterialItem> CreateMaterialAsync(CreateMaterialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "code/name 必填", 400);
        if (request.SearchCode is { Length: > 32 })
            throw new DomainException("VALIDATION_ERROR", "searchCode 最长 32", 400);

        if (await _db.Materials.AnyAsync(m => m.Code == request.Code))
            throw new DomainException("MATERIAL_CODE_DUPLICATED", "物料编码已存在", 409);

        var m = new Material
        {
            Code = request.Code,
            Name = request.Name,
            SearchCode = request.SearchCode,
            BatchControlled = request.BatchControlled,
            LabelType = ParseEnum<LabelType>(request.LabelType, "labelType"),
            DefaultUom = ParseUom(request.DefaultUom),
            DefaultQtyPerLabel = ParseQty(request.DefaultQtyPerLabel),
            Status = request.Status == null ? MaterialStatus.ENABLED : ParseEnum<MaterialStatus>(request.Status, "status"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Materials.Add(m);
        await _db.SaveChangesAsync();
        return MapMaterial(m);
    }

    public async Task<MaterialItem> UpdateMaterialAsync(Guid id, UpdateMaterialRequest request)
    {
        var m = await _db.Materials.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "name 必填", 400);

        m.Name = request.Name;
        m.SearchCode = request.SearchCode;
        m.BatchControlled = request.BatchControlled;
        m.LabelType = ParseEnum<LabelType>(request.LabelType, "labelType");
        m.DefaultUom = ParseUom(request.DefaultUom);
        m.DefaultQtyPerLabel = ParseQty(request.DefaultQtyPerLabel);
        m.Status = ParseEnum<MaterialStatus>(request.Status, "status");
        m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapMaterial(m);
    }

    public async Task DeleteMaterialAsync(Guid id)
    {
        var m = await _db.Materials.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);
        var inUse = await _db.Batches.AnyAsync(b => b.MaterialId == id);
        if (inUse)
            throw new DomainException("MATERIAL_IN_USE", "物料被批次引用，禁止删除", 409);

        _db.Materials.Remove(m);
        await _db.SaveChangesAsync();
    }

    // === Warehouses ===
    public async Task<PagedResult<WarehouseItem>> SearchWarehousesAsync(FilterRequest request)
    {
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLowerInvariant();
            query = query.Where(w =>
                w.Code.ToLower().Contains(kw) ||
                w.Name.ToLower().Contains(kw) ||
                (w.SearchCode != null && w.SearchCode.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(w => w.Status == ParseEnum<MaterialStatus>(request.Status, "status"));

        var (_, result) = await _queryService.ApplyAsync(query, request, WarehouseFields, WarehouseSorts, "code", "asc");
        return new PagedResult<WarehouseItem>(result.Items.Select(MapWarehouse).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<WarehouseItem?> GetWarehouseAsync(Guid id)
    {
        var w = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return w == null ? null : MapWarehouse(w);
    }

    public async Task<WarehouseItem> CreateWarehouseAsync(CreateWarehouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "code/name 必填", 400);
        if (await _db.Warehouses.AnyAsync(w => w.Code == request.Code))
            throw new DomainException("WAREHOUSE_CODE_DUPLICATED", "仓库编码已存在", 409);

        var w = new Warehouse
        {
            Code = request.Code,
            Name = request.Name,
            SearchCode = request.SearchCode,
            Status = request.Status == null ? MaterialStatus.ENABLED : ParseEnum<MaterialStatus>(request.Status, "status"),
            MgmtMode = request.MgmtMode == null ? WarehouseMgmtMode.MANUAL : ParseEnum<WarehouseMgmtMode>(request.MgmtMode, "mgmtMode"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Warehouses.Add(w);
        await _db.SaveChangesAsync();
        return MapWarehouse(w);
    }

    public async Task<WarehouseItem> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request)
    {
        var w = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "name 必填", 400);

        w.Name = request.Name;
        w.SearchCode = request.SearchCode;
        w.Status = ParseEnum<MaterialStatus>(request.Status, "status");
        w.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapWarehouse(w);
    }

    public async Task DeleteWarehouseAsync(Guid id)
    {
        var w = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);
        var inUse = await _db.Locations.AnyAsync(l => l.WarehouseId == id);
        if (inUse)
            throw new DomainException("WAREHOUSE_IN_USE", "仓库下有库位，禁止删除", 409);

        _db.Warehouses.Remove(w);
        await _db.SaveChangesAsync();
    }

    // === Locations ===
    public async Task<PagedResult<LocationItem>> SearchLocationsAsync(Guid warehouseId, FilterRequest request)
    {
        var wh = await _db.Warehouses.AsNoTracking()
            .Select(w => new { w.Id, w.Code })
            .FirstOrDefaultAsync(w => w.Id == warehouseId)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);

        var query = _db.Locations.AsNoTracking().Where(l => l.WarehouseId == warehouseId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLowerInvariant();
            query = query.Where(l =>
                l.Code.ToLower().Contains(kw) ||
                (l.SearchCode != null && l.SearchCode.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(l => l.Status == ParseEnum<MaterialStatus>(request.Status, "status"));
        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(l => l.Type == ParseEnum<LocationType>(request.Type, "type"));

        var (_, result) = await _queryService.ApplyAsync(query, request, LocationFields, LocationSorts, "code", "asc");
        var items = result.Items.Select(l => MapLocation(l, wh.Code)).ToList();
        return new PagedResult<LocationItem>(items, result.Total, result.Page, result.PageSize);
    }

    public async Task<LocationItem?> GetLocationAsync(Guid id)
    {
        var l = await _db.Locations.AsNoTracking().Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.Id == id);
        return l == null ? null : MapLocation(l, l.Warehouse?.Code ?? string.Empty);
    }

    public async Task<LocationItem> CreateLocationAsync(Guid warehouseId, CreateLocationRequest request)
    {
        var wh = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId)
            ?? throw new DomainException("WAREHOUSE_NOT_FOUND", "仓库不存在", 404);
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new DomainException("VALIDATION_ERROR", "code 必填", 400);

        if (await _db.Locations.AnyAsync(l => l.WarehouseId == warehouseId && l.Code == request.Code))
            throw new DomainException("LOCATION_CODE_DUPLICATED", "库位编码在仓内已存在", 409);

        var type = ParseEnum<LocationType>(request.Type, "type");
        if (type != LocationType.STAGING && type != LocationType.DEFAULT)
            throw new DomainException("LOCATION_TYPE_INVALID", "库位类型无效", 400);

        var l = new Location
        {
            WarehouseId = warehouseId,
            Code = request.Code,
            SearchCode = request.SearchCode,
            Type = type,
            Status = request.Status == null ? MaterialStatus.ENABLED : ParseEnum<MaterialStatus>(request.Status, "status"),
            Reachability = LocationReachability.UNIVERSAL,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Locations.Add(l);
        await _db.SaveChangesAsync();
        return MapLocation(l, wh.Code);
    }

    public async Task<LocationItem> UpdateLocationAsync(Guid id, UpdateLocationRequest request)
    {
        var l = await _db.Locations.Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("LOCATION_NOT_FOUND", "库位不存在", 404);
        var type = ParseEnum<LocationType>(request.Type, "type");
        if (type != LocationType.STAGING && type != LocationType.DEFAULT)
            throw new DomainException("LOCATION_TYPE_INVALID", "库位类型无效", 400);

        l.Type = type;
        l.SearchCode = request.SearchCode;
        l.Status = ParseEnum<MaterialStatus>(request.Status, "status");
        l.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapLocation(l, l.Warehouse?.Code ?? string.Empty);
    }

    public async Task DeleteLocationAsync(Guid id)
    {
        var l = await _db.Locations.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("LOCATION_NOT_FOUND", "库位不存在", 404);
        // 库存表尚未建（第 5 批），库位引用保护先按“无引用即放行”；有货/被引用场景后置
        _db.Locations.Remove(l);
        await _db.SaveChangesAsync();
    }

    // === Sources ===
    public async Task<PagedResult<SourceItem>> SearchSourcesAsync(FilterRequest request)
    {
        var query = _db.Sources.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLowerInvariant();
            query = query.Where(s =>
                s.Code.ToLower().Contains(kw) ||
                s.Name.ToLower().Contains(kw) ||
                (s.SearchCode != null && s.SearchCode.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(s => s.Status == ParseEnum<MaterialStatus>(request.Status, "status"));
        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(s => s.Type == ParseEnum<SourceType>(request.Type, "type"));

        var (_, result) = await _queryService.ApplyAsync(query, request, SourceFields, SourceSorts, "code", "asc");
        return new PagedResult<SourceItem>(result.Items.Select(MapSource).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<SourceItem?> GetSourceAsync(Guid id)
    {
        var s = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return s == null ? null : MapSource(s);
    }

    public async Task<SourceItem> CreateSourceAsync(CreateSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "code/name 必填", 400);
        var type = ParseEnum<SourceType>(request.Type, "type");
        if (await _db.Sources.AnyAsync(s => s.Type == type && s.Code == request.Code))
            throw new DomainException("SOURCE_CODE_DUPLICATED", "同类型来源编码已存在", 409);

        var s = new Source
        {
            Type = type,
            Code = request.Code,
            Name = request.Name,
            SearchCode = request.SearchCode,
            Status = request.Status == null ? MaterialStatus.ENABLED : ParseEnum<MaterialStatus>(request.Status, "status"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Sources.Add(s);
        await _db.SaveChangesAsync();
        return MapSource(s);
    }

    public async Task<SourceItem> UpdateSourceAsync(Guid id, UpdateSourceRequest request)
    {
        var s = await _db.Sources.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("SOURCE_NOT_FOUND", "来源不存在", 404);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("VALIDATION_ERROR", "name 必填", 400);

        s.Name = request.Name;
        s.SearchCode = request.SearchCode;
        s.Status = ParseEnum<MaterialStatus>(request.Status, "status");
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return MapSource(s);
    }

    public async Task DeleteSourceAsync(Guid id)
    {
        var s = await _db.Sources.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new DomainException("SOURCE_NOT_FOUND", "来源不存在", 404);
        var inUse = await _db.Batches.AnyAsync(b => b.SourceType == s.Type.ToString() && b.SourceCode == s.Code);
        if (inUse)
            throw new DomainException("SOURCE_IN_USE", "来源被批次引用，禁止删除", 409);

        _db.Sources.Remove(s);
        await _db.SaveChangesAsync();
    }

    // === Batches ===
    public async Task<PagedResult<BatchItem>> SearchBatchesAsync(FilterRequest request)
    {
        var query = _db.Batches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.ToLowerInvariant();
            query = query.Where(b =>
                b.BatchNo.ToLower().Contains(kw) ||
                (b.SourceBatchNo != null && b.SourceBatchNo.ToLower().Contains(kw)));
        }
        if (!string.IsNullOrWhiteSpace(request.MaterialId) && Guid.TryParse(request.MaterialId, out var materialId))
            query = query.Where(b => b.MaterialId == materialId);
        if (!string.IsNullOrWhiteSpace(request.MaterialCode))
            query = query.Where(b => b.MaterialCode.ToLower().Contains(request.MaterialCode.ToLowerInvariant()));
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(b => b.Status == ParseEnum<BatchStatus>(request.Status, "status"));

        var (_, result) = await _queryService.ApplyAsync(query, request, BatchFields, BatchSorts, "createdAt", "desc", isTimeBasedList: true);
        return new PagedResult<BatchItem>(result.Items.Select(MapBatch).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<PagedResult<BatchItem>> SearchMaterialBatchesAsync(Guid materialId, FilterRequest request)
    {
        if (!await _db.Materials.AsNoTracking().AnyAsync(m => m.Id == materialId))
            throw new DomainException("MATERIAL_NOT_FOUND", "物料不存在", 404);

        var query = _db.Batches.AsNoTracking().Where(b => b.MaterialId == materialId).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(b => b.Status == ParseEnum<BatchStatus>(request.Status, "status"));

        var (_, result) = await _queryService.ApplyAsync(query, request, BatchFields, BatchSorts, "createdAt", "desc", isTimeBasedList: true);
        return new PagedResult<BatchItem>(result.Items.Select(MapBatch).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<BatchItem?> GetBatchAsync(Guid id)
    {
        var b = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return b == null ? null : MapBatch(b);
    }

    // === Mapping / helpers ===
    private static MaterialItem MapMaterial(Material m) =>
        new(m.Id, m.Code, m.Name, m.SearchCode, m.BatchControlled, m.LabelType.ToString(), m.DefaultUom,
            m.DefaultQtyPerLabel?.ToString("F4", CultureInfo.InvariantCulture), m.Status.ToString(), m.CreatedAt, m.UpdatedAt);

    private static WarehouseItem MapWarehouse(Warehouse w) =>
        new(w.Id, w.Code, w.Name, w.SearchCode, w.Status.ToString(), w.MgmtMode.ToString(), w.CreatedAt);

    private static LocationItem MapLocation(Location l, string warehouseCode) =>
        new(l.Id, l.WarehouseId, warehouseCode, l.Code, l.SearchCode, l.Type.ToString(), l.Status.ToString(), l.Reachability.ToString(), l.CreatedAt);

    private static SourceItem MapSource(Source s) =>
        new(s.Id, s.Type.ToString(), s.Code, s.Name, s.SearchCode, s.Status.ToString(), s.CreatedAt);

    private static BatchItem MapBatch(Batch b) =>
        new(b.Id, b.MaterialId, b.MaterialCode, b.BatchNo, b.SourceBatchNo, b.SourceType, b.SourceCode,
            b.ProductionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            b.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), b.Status.ToString(), b.CreatedAt);

    private static TEnum ParseEnum<TEnum>(string? value, string fieldName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("VALIDATION_ERROR", $"{fieldName} 必填", 400);
        if (!Enum.TryParse<TEnum>(value, true, out var result))
            throw new DomainException("VALIDATION_ERROR", $"{fieldName} 值无效：{value}", 400);
        return result;
    }

    private static string ParseUom(string? value)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CT", "PC", "BOX", "KG", "G", "L", "M" };
        if (string.IsNullOrWhiteSpace(value) || !allowed.Contains(value))
            throw new DomainException("VALIDATION_ERROR", $"defaultUom 值无效：{value}", 400);
        return value.ToUpperInvariant();
    }

    private static decimal? ParseQty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
            throw new DomainException("VALIDATION_ERROR", $"defaultQtyPerLabel 必须为正数：{value}", 400);
        return qty;
    }
}

