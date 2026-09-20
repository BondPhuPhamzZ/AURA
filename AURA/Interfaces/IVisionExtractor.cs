using System.Threading.Tasks;
using AURA.Models;

namespace AURA.Interfaces
{
    public interface IVisionExtractor
    {
        Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath, CancellationToken cancellationToken = default);
    }
}
