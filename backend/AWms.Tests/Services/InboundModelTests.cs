using AWms.Domain.Entities;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AWms.Tests.Services;

public class InboundModelTests
{
    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    [Fact]
    public void 第4批关键唯一约束_已注册到EF模型()
    {
        using var db = CreateDb();

        AssertUnique<InboundOrderLine>(db, "OrderId", "LineNo");
        AssertUnique<UniqueCode>(db, "Code");
        AssertUnique<ReceiptLine>(db, "ReceiptId", "LineNo");
        AssertUnique<QualityCheck>(db, "ReceiptLineId");
        AssertUnique<PutawayRecord>(db, "ReceiptLineId");
        AssertUnique<StockSubject>(db, "WarehouseId", "MaterialId", "BatchId", "Status");
        AssertUnique<PhysicalInventory>(db, "LocationId", "SubjectId");
        AssertUnique<StockLedger>(db, "TxnGroupId", "Seq");
        AssertUnique<PrintJobItem>(db, "PrintJobId", "Seq");
        AssertUnique<IdempotencyRecord>(db, "Key");
    }

    private static void AssertUnique<TEntity>(AWmsDbContext db, params string[] propertyNames)
    {
        var entity = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity not registered: {typeof(TEntity).Name}");
        var found = entity.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

        Assert.True(found, $"{typeof(TEntity).Name} unique index ({string.Join(",", propertyNames)}) missing");
    }
}
