using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ExpenseTracker.Application.Interfaces;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    /// <summary>SQL Server error numbers for a duplicate key and a unique constraint.</summary>
    private const int DuplicateKeyError = 2601;
    private const int UniqueConstraintError = 2627;

    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public bool IsUniqueViolation(Exception exception) =>
        exception is DbUpdateException { InnerException: SqlException sqlException }
        && sqlException.Number is DuplicateKeyError or UniqueConstraintError;
}
