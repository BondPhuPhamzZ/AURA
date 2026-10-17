using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptAmountResolverTests
{
    [Fact]
    public void Keeps_user_amount_when_it_was_provided()
    {
        var facts = new ReceiptExtractionDto { TotalAmount = 120_000m };

        var result = ReceiptAmountResolver.Resolve(100_000m, facts);

        Assert.Equal(100_000m, result.PolicyAmount);
        Assert.Equal(AmountSources.UserProvided, result.Source);
    }

    [Fact]
    public void Uses_printed_ai_total_when_user_omits_amount()
    {
        var facts = new ReceiptExtractionDto { TotalAmount = 120_000m };

        var result = ReceiptAmountResolver.Resolve(0, facts);

        Assert.Equal(120_000m, result.PolicyAmount);
        Assert.Equal(AmountSources.AiExtracted, result.Source);
    }

    [Fact]
    public void Fails_closed_when_neither_source_has_an_amount()
    {
        var result = ReceiptAmountResolver.Resolve(0, new ReceiptExtractionDto());

        Assert.Equal(0, result.PolicyAmount);
        Assert.Equal(AmountSources.Unavailable, result.Source);
    }
}
