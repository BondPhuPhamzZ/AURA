using AURA.Services;

namespace AURA.Interfaces;

public interface IInvoiceIngestionService
{
    Task<InvoiceIngestionResult> IngestAsync(InvoiceIngestionCommand command,
        CancellationToken cancellationToken = default);
}
