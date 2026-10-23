using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AURA.Models;
using AURA.Interfaces;
using AURA.Data;

namespace AURA.Services
{
    public class ReimbursementRepository : IReimbursementRepository
    {
        private readonly AppDbContext _context;

        public ReimbursementRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReimbursementRequest>> GetAllRequestsAsync()
        {
            return await _context.ReimbursementRequests
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task AddRequestWithAuditAsync(ReimbursementRequest request, string action, string details)
        {
            await _context.ReimbursementRequests.AddAsync(request);
            await _context.AuditLogs.AddAsync(new AuditLog
            {
                RequestId = request.Id,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task<ReceiptIntakePersistenceResult> TryAddRequestWithAuditAsync(
            ReimbursementRequest request, string action, string details, int maxOutstandingReceipts,
            CancellationToken cancellationToken)
        {
            if (maxOutstandingReceipts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxOutstandingReceipts));

            await using var transaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = N'AURA:ReceiptIntakeCapacity',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 10000;
                SELECT @lockResult;
                """;
            var lockResult = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (lockResult < 0)
                throw new TimeoutException("Timed out acquiring the shared receipt intake capacity lock.");

            var persisted = await _context.ReimbursementRequests.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
            if (persisted is not null)
                return ReceiptIntakePersistenceResult.AlreadyExists;

            var outstanding = await _context.ReimbursementRequests.CountAsync(item =>
                item.ProcessingState == ProcessingStates.Pending ||
                item.ProcessingState == ProcessingStates.Processing, cancellationToken);
            if (outstanding >= maxOutstandingReceipts)
                return ReceiptIntakePersistenceResult.CapacityReached;

            await _context.ReimbursementRequests.AddAsync(request, cancellationToken);
            await _context.AuditLogs.AddAsync(new AuditLog
            {
                RequestId = request.Id,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ReceiptIntakePersistenceResult.Added;
        }

        public async Task<ReimbursementRequest?> GetRequestByIdAsync(string id)
        {
            return await _context.ReimbursementRequests.FindAsync(id);
        }

        public Task<ReimbursementRequest?> GetPersistedRequestByIdAsync(string id)
        {
            // Unlike FindAsync, this must bypass entities left in the change tracker
            // after a failed SaveChanges. Intake uses it to verify ambiguous writes.
            return _context.ReimbursementRequests.AsNoTracking()
                .SingleOrDefaultAsync(request => request.Id == id);
        }

        public async Task UpdateRequestAsync(ReimbursementRequest request)
        {
            _context.ReimbursementRequests.Update(request);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details)
        {
            _context.ReimbursementRequests.Update(request);
            await _context.AuditLogs.AddAsync(new AuditLog
            {
                RequestId = request.Id,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRequestsWithAuditAsync(
            IEnumerable<(ReimbursementRequest Request, string Details)> requests, string action)
        {
            var items = requests.ToList();
            if (items.Count == 0) return;

            _context.ReimbursementRequests.UpdateRange(items.Select(x => x.Request));
            await _context.AuditLogs.AddRangeAsync(items.Select(x => new AuditLog
            {
                RequestId = x.Request.Id,
                Action = action,
                Details = x.Details,
                Timestamp = DateTime.UtcNow
            }));
            await _context.SaveChangesAsync();
        }

        public Task<bool> ExistsByFileHashAsync(string sha256)
        {
            return _context.ReimbursementRequests.AnyAsync(x => x.FileSha256 == sha256);
        }
    }
}
