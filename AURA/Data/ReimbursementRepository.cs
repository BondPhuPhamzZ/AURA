using System.Collections.Generic;
using AURA.Models;

namespace AURA.Data
{
    public interface IReimbursementRepository
    {
        List<ReimbursementRequest> GetAllRequests();
        void AddRequest(ReimbursementRequest request);
    }

    public class MockReimbursementRepository : IReimbursementRepository
    {
        private readonly List<ReimbursementRequest> _requests = new();

        public List<ReimbursementRequest> GetAllRequests() => _requests;

        public void AddRequest(ReimbursementRequest request)
        {
            _requests.Add(request);
        }
    }
}
