using System;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using AURA.Data;
using Microsoft.EntityFrameworkCore;

namespace AURA.Services
{
    public class AuditLogger : IAuditLogger
    {
        private readonly AppDbContext _context;

        public AuditLogger(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogActionAsync(string requestId, string action, string details)
        {
            var log = new AuditLog
            {
                RequestId = requestId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        public Task<List<AuditLog>> GetRecentAsync(int limit = 100)
        {
            return _context.AuditLogs.AsNoTracking()
                .OrderByDescending(x => x.Timestamp)
                .Take(Math.Clamp(limit, 1, 500))
                .ToListAsync();
        }

        public Task<List<AuditLog>> GetByRequestIdAsync(string requestId)
        {
            return _context.AuditLogs.AsNoTracking()
                .Where(x => x.RequestId == requestId)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync();
        }
    }
}
