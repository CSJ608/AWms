using System.Data;
using AWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AWms.Infrastructure.Services;

internal sealed class BusinessTransaction : IAsyncDisposable
{
    private readonly AWmsDbContext _db;
    private readonly bool _ownsTransaction;
    private readonly string? _savepoint;
    private bool _completed;

    private BusinessTransaction(
        AWmsDbContext db,
        IDbContextTransaction transaction,
        bool ownsTransaction,
        string? savepoint)
    {
        _db = db;
        Transaction = transaction;
        _ownsTransaction = ownsTransaction;
        _savepoint = savepoint;
    }

    public IDbContextTransaction Transaction { get; }

    public static async Task<BusinessTransaction> BeginAsync(
        AWmsDbContext db,
        IsolationLevel isolationLevel,
        CancellationToken ct)
    {
        var current = db.Database.CurrentTransaction;
        if (current == null)
        {
            var transaction = await db.Database.BeginTransactionAsync(isolationLevel, ct);
            return new BusinessTransaction(db, transaction, true, null);
        }

        var savepoint = $"business_{Guid.CreateVersion7():N}";
        await current.CreateSavepointAsync(savepoint, ct);
        return new BusinessTransaction(db, current, false, savepoint);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_completed)
            return;

        if (_ownsTransaction)
            await Transaction.CommitAsync(ct);
        else
            await Transaction.ReleaseSavepointAsync(_savepoint!, ct);

        _completed = true;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_completed)
            return;

        if (_ownsTransaction)
            await Transaction.RollbackAsync(ct);
        else
            await Transaction.RollbackToSavepointAsync(_savepoint!, ct);

        _db.ChangeTracker.Clear();
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed)
            await RollbackAsync(CancellationToken.None);
        if (_ownsTransaction)
            await Transaction.DisposeAsync();
    }
}
