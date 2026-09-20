using System;
using System.Threading.Tasks;
using AURA.Interfaces;
using AURA.Models;
using AURA.Data;

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
    }
}
