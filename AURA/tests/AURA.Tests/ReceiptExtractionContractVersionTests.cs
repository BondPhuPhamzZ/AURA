using System.Text.Json;
using System.Text.Json.Nodes;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptExtractionContractVersionTests
{
    [Fact]
    public void Complete_response_missing_only_protocol_version_is_marked_as_inferred()
    {
        var response = CompleteResponse();
        response.Remove("evidenceContractVersion");

        var facts = ReceiptExtractionContract.ParseFacts(response.ToJsonString());

        Assert.Equal(2, facts.EvidenceContractVersion);
        Assert.True(facts.EvidenceContractVersionInferred);
        Assert.Null(facts.TotalAmount);
        Assert.NotEmpty(ReceiptSemanticValidator.Validate(facts)); // Missing payable evidence stays under human review.
    }

    [Fact]
    public void Missing_evidence_field_cannot_be_repaired_by_version_inference()
    {
        var response = CompleteResponse();
        response.Remove("evidenceContractVersion");
        response.Remove("totalAmountEvidence");

        Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(response.ToJsonString()));
    }

    [Fact]
    public void Explicit_wrong_version_is_rejected()
    {
        var response = CompleteResponse();
        response["evidenceContractVersion"] = 1;

        Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(response.ToJsonString()));
    }

    private static JsonObject CompleteResponse()
    {
        var schema = JsonSerializer.SerializeToElement(ReceiptExtractionContract.BuildResponseSchema());
        var response = new JsonObject();
        foreach (var field in schema.GetProperty("required").EnumerateArray())
        {
            var name = field.GetString()!;
            response[name] = name switch
            {
                "evidenceContractVersion" => JsonValue.Create(2),
                "documentType" => JsonValue.Create("OTHER"),
                "documentStatus" => JsonValue.Create("UNKNOWN"),
                "totalAmountSource" => JsonValue.Create("NOT_VISIBLE"),
                "lineItems" or "missingFields" or "warnings" or "suspiciousSignals" => new JsonArray(),
                "confidence" => JsonValue.Create(0.5),
                _ => null
            };
        }
        return response;
    }
}
