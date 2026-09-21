using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public async Task<ReimbursementRequest?> GetRequestByIdAsync(string id)
        {
            return await _context.ReimbursementRequests.FindAsync(id);
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

        public Task<bool> ExistsByFileHashAsync(string sha256)
        {
            return _context.ReimbursementRequests.AnyAsync(x => x.FileSha256 == sha256);
        }
    }
}
