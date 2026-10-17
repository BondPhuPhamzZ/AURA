using AURA.Models;

namespace AURA.Services;

public static class ReceiptAmountResolver
{
    public static (decimal PolicyAmount, string Source) Resolve(
        decimal submittedAmount, ReceiptExtractionDto facts)
    {
        if (submittedAmount > 0)
            return (submittedAmount, AmountSources.UserProvided);

        if (facts.TotalAmount is > 0)
            return (facts.TotalAmount.Value, AmountSources.AiExtracted);

        return (0, AmountSources.Unavailable);
    }
}
