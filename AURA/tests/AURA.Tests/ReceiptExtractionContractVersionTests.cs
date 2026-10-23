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
    public void Explicit_version_does_not_hide_missing_required_evidence()
    {
        var response = CompleteResponse();
        response.Remove("totalAmountEvidence");
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("MISSING_REQUIRED_FIELD", diagnostic.ReasonCode);
        Assert.Equal("totalAmountEvidence", diagnostic.SchemaField);
    }

    [Fact]
    public void Explicit_version_requires_complete_line_item_shape()
    {
        var response = CompleteResponse();
        response["lineItems"] = new JsonArray(new JsonObject
        {
            ["description"] = "Synthetic item", ["quantity"] = 1,
            ["unitPrice"] = 100
        });
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("MISSING_LINE_ITEM_FIELD", diagnostic.ReasonCode);
        Assert.Equal("amount", diagnostic.SchemaField);
    }

    [Fact]
    public void Required_array_cannot_be_null_even_with_explicit_version()
    {
        var response = CompleteResponse();
        response["warnings"] = null;
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("INVALID_REQUIRED_ARRAY", diagnostic.ReasonCode);
        Assert.Equal("warnings", diagnostic.SchemaField);
    }

    [Fact]
    public void Model_cannot_supply_backend_only_audit_or_privacy_fields()
    {
        var response = CompleteResponse();
        response["validationIssues"] = new JsonArray("customer@example.test");
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("UNEXPECTED_FIELD", diagnostic.ReasonCode);
        Assert.Equal("<none>", diagnostic.SchemaField);
        Assert.DoesNotContain("customer@example.test", string.Join('|', diagnostic.ReasonCode, diagnostic.SchemaField));
    }

    [Fact]
    public void Model_cannot_supply_extra_or_case_duplicate_fields()
    {
        var response = CompleteResponse();
        response["TOTALAMOUNT"] = 1;
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("DUPLICATE_FIELD", diagnostic.ReasonCode);
        Assert.Equal("<none>", diagnostic.SchemaField);
    }

    [Fact]
    public void Model_cannot_supply_extra_line_item_fields()
    {
        var response = CompleteResponse();
        response["lineItems"] = new JsonArray(new JsonObject
        {
            ["description"] = "Synthetic item", ["quantity"] = 1,
            ["unitPrice"] = 100, ["amount"] = 100,
            ["policyOverride"] = "AUTO_APPROVE"
        });
        var content = response.ToJsonString();

        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));
        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("UNEXPECTED_LINE_ITEM_FIELD", diagnostic.ReasonCode);
        Assert.Equal("lineItems", diagnostic.SchemaField);
    }

    [Fact]
    public void Explicit_wrong_version_is_rejected()
    {
        var response = CompleteResponse();
        response["evidenceContractVersion"] = 1;

        Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(response.ToJsonString()));
    }

    [Fact]
    public void Diagnostic_identifies_missing_schema_field_without_returning_values()
    {
        var response = CompleteResponse();
        response.Remove("evidenceContractVersion");
        response.Remove("totalAmountEvidence");
        response["merchantName"] = "PRIVATE MERCHANT NAME";
        var content = response.ToJsonString();
        var error = Assert.ThrowsAny<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));

        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("MISSING_VERSION_AND_FIELD", diagnostic.ReasonCode);
        Assert.Equal("totalAmountEvidence", diagnostic.SchemaField);
        Assert.DoesNotContain("PRIVATE MERCHANT NAME", diagnostic.ToString());
    }

    [Fact]
    public void Diagnostic_distinguishes_invalid_json_syntax()
    {
        const string content = "{\"merchantName\":\"PRIVATE MERCHANT NAME\",\"lineItems\":[{";
        var error = Assert.ThrowsAny<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));

        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("INVALID_JSON_SYNTAX", diagnostic.ReasonCode);
        Assert.Equal("<none>", diagnostic.SchemaField);
        Assert.DoesNotContain("PRIVATE MERCHANT NAME", diagnostic.ToString());
    }

    [Fact]
    public void Diagnostic_distinguishes_unsupported_contract_version()
    {
        var response = CompleteResponse();
        response["evidenceContractVersion"] = 1;
        var content = response.ToJsonString();
        var error = Assert.Throws<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));

        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("UNSUPPORTED_CONTRACT_VERSION", diagnostic.ReasonCode);
        Assert.Equal("evidenceContractVersion", diagnostic.SchemaField);
    }

    [Fact]
    public void Diagnostic_reports_only_known_field_for_invalid_value_type()
    {
        var response = CompleteResponse();
        response["totalAmount"] = "PRIVATE INVALID VALUE";
        var content = response.ToJsonString();
        var error = Assert.ThrowsAny<JsonException>(() => ReceiptExtractionContract.ParseFacts(content));

        var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(content, error);

        Assert.Equal("FIELD_TYPE_OR_VALUE", diagnostic.ReasonCode);
        Assert.Equal("totalAmount", diagnostic.SchemaField);
        Assert.DoesNotContain("PRIVATE INVALID VALUE", diagnostic.ToString());
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
