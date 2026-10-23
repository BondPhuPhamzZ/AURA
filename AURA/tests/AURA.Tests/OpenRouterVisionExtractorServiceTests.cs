using System.Net;
using System.Text;
using System.Text.Json;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class OpenRouterVisionExtractorServiceTests
{
    [Fact]
    public void Defaults_to_benchmarked_flash_model_with_reasoning_disabled()
    {
        var options = new OpenRouterOptions();

        Assert.Equal("qwen/qwen3.8-flash", options.Model);
        Assert.Equal("none", options.ReasoningEffort);
        Assert.True(options.DenyProviderDataCollection);
        Assert.False(options.RequireZeroDataRetention);
    }

    [Fact]
    public void Accepts_only_documented_reasoning_effort_values()
    {
        var supported = new string?[] { null, "", "none", "minimal", "low", "medium", "high", "xhigh", "max", " NONE " };

        Assert.All(supported, value => Assert.True(OpenRouterOptions.IsSupportedReasoningEffort(value)));
        Assert.False(OpenRouterOptions.IsSupportedReasoningEffort("off"));
        Assert.False(OpenRouterOptions.IsSupportedReasoningEffort("auto"));
    }

    [Fact]
    public async Task Preserves_sanitized_429_diagnostics_and_requests_routing_metadata()
    {
        var root = CreateFixtureRoot();
        try
        {
            string? metadataHeader = null;
            var handler = new StubHandler(request =>
            {
                metadataHeader = request.Headers.TryGetValues("X-OpenRouter-Metadata", out var values)
                    ? values.Single()
                    : null;
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        "{\"error\":{\"message\":\"Provider capacity exceeded\",\"metadata\":{\"provider_name\":\"Parasail\"}}}",
                        Encoding.UTF8, "application/json")
                };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(45));
                response.Headers.TryAddWithoutValidation("X-Request-Id", "req-safe-123");
                return Task.FromResult(response);
            });
            var options = Microsoft.Extensions.Options.Options.Create(
                new OpenRouterOptions { ApiKey = "test-key" });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_RATE_LIMIT", exception.Code);
            Assert.Equal(TimeSpan.FromSeconds(45), exception.RetryAfter);
            Assert.Equal("Parasail", exception.Provider);
            Assert.Equal("req-safe-123", exception.RequestId);
            Assert.Equal("enabled", metadataHeader);
            Assert.DoesNotContain("Alibaba", exception.UserMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Provider_error_text_and_invalid_request_id_are_not_exposed()
    {
        var root = CreateFixtureRoot();
        try
        {
            const string privateValue = "customer@example.test";
            var logger = new CollectingLogger();
            var handler = new StubHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"error\":{\"message\":\"Receipt email customer@example.test\",\"metadata\":{\"provider_name\":\"Unknown customer@example.test\"}}}",
                        Encoding.UTF8, "application/json")
                };
                response.Headers.TryAddWithoutValidation("X-Request-Id", privateValue);
                return Task.FromResult(response);
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), logger);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_REQUEST_INVALID", exception.Code);
            Assert.Null(exception.RequestId);
            Assert.DoesNotContain(privateValue, exception.UserMessage);
            Assert.DoesNotContain(privateValue, string.Join(' ', logger.Messages));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Sends_image_to_openrouter_chat_completions_with_configured_model()
    {
        var root = CreateFixtureRoot();
        try
        {
            string? requestJson = null;
            Uri? requestUri = null;
            var handler = new StubHandler(async request =>
            {
                requestUri = request.RequestUri;
                requestJson = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"No endpoints found\"}}", Encoding.UTF8, "application/json")
                };
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://openrouter.ai/api/v1/",
                ChatCompletionsPath = "chat/completions",
                Model = "qwen/qwen3-vl-8b-instruct",
                PolicyPath = "BUSINESS_RULES.md",
                TimeoutSeconds = 90,
                MaxOutputTokens = 4096,
                ReasoningEffort = "none",
                HttpReferer = "https://github.com/BondPhuPhamzZ/AURA",
                AppTitle = "AURA - The Escalation Referee"
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_MODEL_UNAVAILABLE", exception.Code);
            Assert.Contains(options.Value.Model, exception.UserMessage);
            Assert.Equal("https://openrouter.ai/api/v1/chat/completions", requestUri?.ToString());
            Assert.Contains($"\"model\":\"{options.Value.Model}\"", requestJson);
            Assert.Contains("\"type\":\"text\"", requestJson);
            Assert.Contains("\"type\":\"image_url\"", requestJson);
            Assert.Contains("PRINTED_FINAL_TOTAL", requestJson, StringComparison.Ordinal);
            using var payload = JsonDocument.Parse(requestJson!);
            var rootElement = payload.RootElement;
            var systemPrompt = rootElement.GetProperty("messages")[0].GetProperty("content").GetString();
            Assert.Contains("BEGIN_RECEIPT_EXTRACTION_CONTRACT", systemPrompt, StringComparison.Ordinal);
            Assert.Equal(4096, rootElement.GetProperty("max_tokens").GetInt32());
            Assert.Equal("none", rootElement.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.Equal("deny", rootElement.GetProperty("provider").GetProperty("data_collection").GetString());
            Assert.False(rootElement.GetProperty("provider").GetProperty("zdr").GetBoolean());
            Assert.Equal("response-healing",
                rootElement.GetProperty("plugins")[0].GetProperty("id").GetString());
            var responseFormat = rootElement.GetProperty("response_format");
            Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
            var jsonSchema = responseFormat.GetProperty("json_schema");
            Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
            var schema = jsonSchema.GetProperty("schema");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            var required = schema.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString()!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var properties = schema.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(properties, required);
            Assert.True(schema.GetProperty("properties").TryGetProperty("receiptNumber", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("transactionReference", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("discountAmount", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("evidenceContractVersion", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("totalAmountSource", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("totalAmountEvidence", out _));
            Assert.True(schema.GetProperty("properties").TryGetProperty("invoiceDateEvidence", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task System_prompt_delimits_extraction_rules_and_excludes_deterministic_policy()
    {
        const string policy = """
            ## 1. Role
            Extract visible receipt evidence only.

            ## 4. Output signals
            Use stable signal codes.

            ## 5. Deterministic policy applied by the application
            INTERNAL_POLICY_SENTINEL: never send this section to the vision model.

            ## 6. Output quality checklist
            CHECKLIST_SENTINEL: backend owns these validations.
            """;
        var root = CreateFixtureRoot(policy);
        try
        {
            string? requestJson = null;
            var handler = new StubHandler(async request =>
            {
                requestJson = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"No endpoints found\"}}",
                        Encoding.UTF8, "application/json")
                };
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            using var payload = JsonDocument.Parse(requestJson!);
            var systemPrompt = payload.RootElement.GetProperty("messages")[0]
                .GetProperty("content").GetString()!;
            Assert.Contains("SECURITY BOUNDARY - HIGHEST PRIORITY", systemPrompt, StringComparison.Ordinal);
            Assert.Contains("PROMPT_INJECTION", systemPrompt, StringComparison.Ordinal);
            Assert.Contains("BEGIN_RECEIPT_EXTRACTION_CONTRACT", systemPrompt, StringComparison.Ordinal);
            Assert.Contains("Extract visible receipt evidence only.", systemPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("INTERNAL_POLICY_SENTINEL", systemPrompt, StringComparison.Ordinal);
            Assert.DoesNotContain("CHECKLIST_SENTINEL", systemPrompt, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Strict_zdr_option_sets_provider_zdr_filter()
    {
        var root = CreateFixtureRoot();
        try
        {
            string? requestJson = null;
            var handler = new StubHandler(async request =>
            {
                requestJson = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"No endpoints found\"}}",
                        Encoding.UTF8, "application/json")
                };
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                RequireZeroDataRetention = true,
                DenyProviderDataCollection = true
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            using var payload = JsonDocument.Parse(requestJson!);
            var provider = payload.RootElement.GetProperty("provider");
            Assert.True(provider.GetProperty("zdr").GetBoolean());
            Assert.Equal("deny", provider.GetProperty("data_collection").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Parses_strict_qwen_json_response_without_calling_external_api()
    {
        var root = CreateFixtureRoot();
        try
        {
            var factsJson = JsonSerializer.Serialize(new
            {
                evidenceContractVersion = 2,
                documentType = "RETAIL_RECEIPT",
                documentStatus = "ISSUED",
                merchantName = "Cửa hàng thử nghiệm",
                taxId = (string?)null,
                merchantId = (string?)null,
                terminalId = (string?)null,
                platformName = (string?)null,
                orderId = (string?)null,
                bookingId = (string?)null,
                shippingTrackingCode = (string?)null,
                shippingProvider = (string?)null,
                orderStatus = "PAID",
                invoiceNumber = "HD-001",
                receiptNumber = (string?)null,
                transactionReference = (string?)null,
                taxAuthorityCode = (string?)null,
                invoiceSerial = (string?)null,
                documentNumber = (string?)null,
                posNumber = (string?)null,
                invoiceDate = "2026-09-22",
                invoiceDateEvidence = "22/09/2026",
                transactionDate = (string?)null,
                transactionDateEvidence = (string?)null,
                completionDate = (string?)null,
                invoiceTime = "10:30",
                currency = "VND",
                subtotal = 100000m,
                discountAmount = (decimal?)null,
                tax = 0m,
                totalAmount = 100000m,
                totalAmountSource = "PRINTED_FINAL_TOTAL",
                totalAmountEvidence = "TỔNG THANH TOÁN 100.000 VND",
                lineItems = new[] { new { description = "Văn phòng phẩm", quantity = 1m, unitPrice = 100000m, amount = 100000m } },
                missingFields = Array.Empty<string>(),
                warnings = Array.Empty<string>(),
                suspiciousSignals = Array.Empty<string>(),
                confidence = 0.97
            });
            var responseJson = JsonSerializer.Serialize(new
            {
                choices = new[] { new { finish_reason = "stop", message = new { content = factsJson } } }
            });
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                Model = "qwen/qwen3-vl-8b-instruct",
                PolicyPath = "BUSINESS_RULES.md"
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal("RETAIL_RECEIPT", result.DocumentType);
            Assert.Equal(100000m, result.TotalAmount);
            Assert.Single(result.LineItems);
            Assert.Equal(0.97, result.Confidence, 3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Tolerates_qwen_wrapper_numeric_strings_and_provider_metadata_without_external_api()
    {
        var root = CreateFixtureRoot();
        try
        {
            const string factsJson = """
                Analysis complete.
                ```json
                {
                  "evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Cửa hàng thử nghiệm",
                  "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
                  "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":"PAID",
                  "invoiceNumber":"HD-002","receiptNumber":null,"transactionReference":null,"taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,
                  "invoiceDate":"2026-09-22","invoiceDateEvidence":"22/09/2026","transactionDate":null,"transactionDateEvidence":null,
                  "completionDate":null,"invoiceTime":"10:30","currency":"VND","subtotal":"88000",
                  "discountAmount":null,"tax":"0","totalAmount":"88000","totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng cộng 88.000 VND","lineItems":[{"description":"Phở bò","quantity":"1",
                  "unitPrice":"88000","amount":"88000"}],"missingFields":[],"warnings":[],
                  "suspiciousSignals":[],"confidence":"0.91"
                }
                ```
                """;
            var responseJson = JsonSerializer.Serialize(new
            {
                choices = new[] { new { finish_reason = "stop", message = new { content = factsJson } } },
                openrouter_metadata = new
                {
                    endpoints = new { available = new[] { new { selected = true, provider = "Alibaba Cloud" } } }
                }
            });
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", Model = "qwen/qwen3-vl-8b-instruct", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(88000m, result.TotalAmount);
            Assert.Equal(0.91, result.Confidence, 3);
            Assert.Single(result.LineItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Retries_once_when_qwen_returns_truncated_structured_output()
    {
        var root = CreateFixtureRoot();
        try
        {
            var attempts = 0;
            string? secondRequest = null;
            const string validFacts = """
                {"evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Cửa hàng thử nghiệm",
                "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
                "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":"PAID",
                "invoiceNumber":"HD-003","receiptNumber":null,"transactionReference":null,"taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,
                "invoiceDate":"2026-09-22","invoiceDateEvidence":"22/09/2026","transactionDate":null,"transactionDateEvidence":null,
                "completionDate":null,"invoiceTime":"10:30","currency":"VND","subtotal":100000,
                "discountAmount":null,"tax":0,"totalAmount":100000,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng cộng 100.000 VND","lineItems":[{"description":"Văn phòng phẩm","quantity":1,
                "unitPrice":100000,"amount":100000}],"missingFields":[],"warnings":[],
                "suspiciousSignals":[],"confidence":0.95}
                """;
            var handler = new StubHandler(request =>
            {
                attempts++;
                if (attempts == 2)
                    secondRequest = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var content = attempts == 1
                    ? "{\"documentType\":\"RETAIL_RECEIPT\",\"lineItems\":[{"
                    : validFacts;
                var responseJson = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content } } }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", Model = "qwen/qwen3-vl-8b-instruct", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(2, attempts);
            Assert.Contains("Previous response failed the required JSON schema", secondRequest);
            Assert.Contains("evidenceContractVersion=2", secondRequest);
            Assert.Equal("HD-003", result.InvoiceNumber);
            Assert.Equal(100000m, result.TotalAmount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rejects_missing_contract_version_before_semantic_repair()
    {
        var root = CreateFixtureRoot();
        try
        {
            var attempts = 0;
            var handler = new StubHandler(_ =>
            {
                attempts++;
                var body = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new
                    {
                        content = "{\"documentType\":\"RETAIL_RECEIPT\",\"totalAmount\":100000}"
                    } } }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var failure = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_SCHEMA_MISMATCH", failure.Code);
            Assert.Equal(2, attempts); // schema retry only; semantic repair needs a parsed DTO.
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Two_token_non_object_response_retries_once_then_fails_without_semantic_repair()
    {
        var root = CreateFixtureRoot();
        try
        {
            var attempts = 0;
            var logger = new CollectingLogger();
            var handler = new StubHandler(_ =>
            {
                attempts++;
                var body = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content = "[]" } } },
                    usage = new
                    {
                        prompt_tokens = 700L, completion_tokens = 2L, total_tokens = 702L,
                        completion_tokens_details = new { reasoning_tokens = 0L }
                    },
                    openrouter_metadata = new
                    {
                        endpoints = new
                        {
                            available = new[] { new { selected = true, provider = "Alibaba Cloud" } }
                        }
                    }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), logger);

            var failure = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_SCHEMA_MISMATCH", failure.Code);
            Assert.Equal(2, attempts);
            Assert.Null(failure.InnerException); // Do not carry provider-derived JSON into worker logs.
            Assert.Contains(logger.Messages, message => message.Contains("provider kind=Alibaba", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message => message.Contains("completion=2", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, message => message.Contains("reasoning=0", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Applies_the_same_semantic_repair_contract_on_openrouter()
    {
        var root = CreateFixtureRoot();
        try
        {
            var attempts = 0;
            var handler = new StubHandler(_ =>
            {
                attempts++;
                var content = attempts == 1 ? FaultyTc01Json : CorrectedTc01Json;
                var responseJson = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content } } }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                });
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", Model = "qwen/qwen3-vl-8b-instruct", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(2, attempts);
            Assert.Equal("ECOMMERCE", result.DocumentType);
            Assert.Equal(295_199m, result.TotalAmount);
            Assert.Empty(result.ValidationIssues);

            var dateAttempts = 0;
            var dateHandler = new StubHandler(async request =>
            {
                dateAttempts++;
                _ = await request.Content!.ReadAsStringAsync();
                var content = dateAttempts == 1 ? MisplacedPaperDateJson : CorrectedPaperDateJson;
                var responseJson = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content } } }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });
            var dateService = new OpenRouterVisionExtractorService(
                new HttpClient(dateHandler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var correctedDate = await dateService.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(1, dateAttempts);
            Assert.Equal("2026-09-29", correctedDate.InvoiceDate);
            Assert.Null(correctedDate.TransactionDate);
            Assert.Empty(correctedDate.ValidationIssues);
            Assert.False(correctedDate.SemanticRepairApplied);
            Assert.Empty(correctedDate.SemanticRepairIssues);

            var discountAttempts = 0;
            string? discountRepairRequestJson = null;
            var discountHandler = new StubHandler(async request =>
            {
                discountAttempts++;
                if (discountAttempts == 2)
                    discountRepairRequestJson = await request.Content!.ReadAsStringAsync();
                var content = discountAttempts == 1 ? VinamilkWithoutStructuredDiscountJson : VinamilkDiscountedJson;
                var responseJson = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { finish_reason = "stop", message = new { content } } }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });
            var discountService = new OpenRouterVisionExtractorService(
                new HttpClient(discountHandler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var correctedDiscount = await discountService.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(2, discountAttempts);
            Assert.Equal(2_828m, correctedDiscount.DiscountAmount);
            Assert.Equal(180_286m, correctedDiscount.TotalAmount);
            Assert.Empty(correctedDiscount.ValidationIssues);
            Assert.True(correctedDiscount.SemanticRepairApplied);
            Assert.Contains(correctedDiscount.SemanticRepairIssues,
                issue => issue.Contains("discountAmount", StringComparison.Ordinal));
            Assert.Contains("discountAmount", discountRepairRequestJson, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private const string FaultyTc01Json = """
        {"evidenceContractVersion":2,"documentType":"RIDE_HAILING","documentStatus":"COMPLETED","merchantName":"Double Fish Việt Nam",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":"SPX Instant",
        "orderId":"SPX-VN2693231211394","bookingId":null,"shippingTrackingCode":"SPX-VN2693231211394",
        "shippingProvider":"SPX Instant","orderStatus":"COMPLETED","invoiceNumber":null,"receiptNumber":null,"transactionReference":null,
        "taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,"invoiceDate":null,"invoiceDateEvidence":null,
        "transactionDate":"2026-09-18","transactionDateEvidence":"18/09/2026","completionDate":"2026-09-18","invoiceTime":"09:45",
        "currency":"VND","subtotal":292.199,"discountAmount":null,"tax":3.0,"totalAmount":295.199,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Thành tiền 295.199 đ",
        "lineItems":[{"description":"Vợt bóng bàn","quantity":1,"unitPrice":292.199,"amount":292.199},
        {"description":"Bảo hiểm người tiêu dùng","quantity":1,"unitPrice":3.0,"amount":3.0}],
        "missingFields":[],"warnings":[],"suspiciousSignals":[],"confidence":0.95}
        """;

    private const string CorrectedTc01Json = """
        {"evidenceContractVersion":2,"documentType":"ECOMMERCE","documentStatus":"COMPLETED","merchantName":"Double Fish Việt Nam",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,"bookingId":null,
        "shippingTrackingCode":"SPX-VN2693231211394","shippingProvider":"SPX Instant","orderStatus":"COMPLETED",
        "invoiceNumber":null,"receiptNumber":null,"transactionReference":null,"taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,
        "invoiceDate":null,"invoiceDateEvidence":null,"transactionDate":"2026-09-18","transactionDateEvidence":"18/09/2026","completionDate":"2026-09-18",
        "invoiceTime":"09:45","currency":"VND","subtotal":295199,"discountAmount":null,"tax":0,"totalAmount":295199,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Thành tiền 295.199 đ",
        "lineItems":[{"description":"Vợt bóng bàn","quantity":1,"unitPrice":292199,"amount":292199},
        {"description":"Bảo hiểm người tiêu dùng","quantity":1,"unitPrice":3000,"amount":3000}],
        "missingFields":[],"warnings":[],"suspiciousSignals":[],"confidence":0.95}
        """;

    private const string MisplacedPaperDateJson = """
        {"evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"HIGHLANDS COFFEE",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
        "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":null,
        "invoiceNumber":null,"receiptNumber":null,"transactionReference":"221196","taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,"invoiceDate":null,"invoiceDateEvidence":null,
        "transactionDate":"2026-09-29","transactionDateEvidence":"29/09/2026","completionDate":null,"invoiceTime":"13:52","currency":"VND",
        "subtotal":59000,"discountAmount":null,"tax":0,"totalAmount":59000,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng cộng 59.000 VND","lineItems":[{"description":"PhinDi Kem Sua L",
        "quantity":1,"unitPrice":59000,"amount":59000}],"missingFields":[],"warnings":[],
        "suspiciousSignals":[],"confidence":0.95}
        """;

    private const string CorrectedPaperDateJson = """
        {"evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"HIGHLANDS COFFEE",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
        "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":null,
        "invoiceNumber":null,"receiptNumber":null,"transactionReference":"221196","taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,"invoiceDate":"2026-09-29","invoiceDateEvidence":"29/09/2026",
        "transactionDate":null,"transactionDateEvidence":null,"completionDate":null,"invoiceTime":"13:52","currency":"VND",
        "subtotal":59000,"discountAmount":null,"tax":0,"totalAmount":59000,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng cộng 59.000 VND","lineItems":[{"description":"PhinDi Kem Sua L",
        "quantity":1,"unitPrice":59000,"amount":59000}],"missingFields":[],"warnings":[],
        "suspiciousSignals":[],"confidence":0.95}
        """;

    private const string VinamilkWithoutStructuredDiscountJson = """
        {"evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Vinamilk",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
        "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":null,
        "invoiceNumber":null,"receiptNumber":"SAL.CH40411260922000147","transactionReference":null,"taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,
        "invoiceDate":"2026-09-22","invoiceDateEvidence":"22/09/2026","transactionDate":null,"transactionDateEvidence":null,"completionDate":null,"invoiceTime":"17:31",
        "currency":"VND","subtotal":183114,"discountAmount":null,"tax":0,"totalAmount":180286,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng thanh toán 180.286 VND",
        "lineItems":[{"description":"Sản phẩm sữa","quantity":1,"unitPrice":183114,"amount":183114}],
        "missingFields":[],"warnings":["Có dòng giảm giá trên hóa đơn"],"suspiciousSignals":[],"confidence":0.95}
        """;

    private const string VinamilkDiscountedJson = """
        {"evidenceContractVersion":2,"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Vinamilk",
        "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
        "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":null,
        "invoiceNumber":null,"receiptNumber":"SAL.CH40411260922000147","transactionReference":null,"taxAuthorityCode":null,"invoiceSerial":null,"documentNumber":null,"posNumber":null,
        "invoiceDate":"2026-09-22","invoiceDateEvidence":"22/09/2026","transactionDate":null,"transactionDateEvidence":null,"completionDate":null,"invoiceTime":"17:31",
        "currency":"VND","subtotal":183114,"discountAmount":2828,"tax":0,"totalAmount":180286,"totalAmountSource":"PRINTED_FINAL_TOTAL","totalAmountEvidence":"Tổng thanh toán 180.286 VND",
        "lineItems":[{"description":"Sản phẩm sữa","quantity":1,"unitPrice":183114,"amount":183114}],
        "missingFields":[],"warnings":[],"suspiciousSignals":[],"confidence":0.95}
        """;

    private static string CreateFixtureRoot(string policy = "Return receipt evidence as JSON.")
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-openrouter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "BUSINESS_RULES.md"), policy);
        File.WriteAllBytes(Path.Combine(root, "receipt.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
        return root;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }

    private sealed class CollectingLogger : ILogger<OpenRouterVisionExtractorService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
