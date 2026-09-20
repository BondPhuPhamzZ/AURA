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
            return await _context.ReimbursementRequests.ToListAsync();
        }

        public async Task AddRequestAsync(ReimbursementRequest request)
        {
            await _context.ReimbursementRequests.AddAsync(request);
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
    }
}
