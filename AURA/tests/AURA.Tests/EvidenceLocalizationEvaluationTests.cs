using System.Security.Cryptography;
using System.Text.Json;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Xunit;
using Xunit.Abstractions;
using static AURA.Tests.EvidenceLocalizationEvaluation;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AURA.Tests;

public sealed class EvidenceLocalizationEvaluationTests(ITestOutputHelper output)
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void Intersection_over_union_is_exact_for_simple_boxes()
    {
        var box = new EvidenceBox(0, 0, 100, 100);
        Assert.Equal(1, IoU(box, box));
        Assert.Equal(0, IoU(box, new EvidenceBox(200, 200, 10, 10)));
        Assert.Equal(0, IoU(box, new EvidenceBox(100, 0, 50, 50))); // touching edges do not overlap
        Assert.Equal(50.0 * 100 / (100 * 100 + 100 * 100 - 50 * 100), IoU(box, new EvidenceBox(50, 0, 100, 100)), 6);
    }

    [Fact]
    public void Every_outcome_is_classified_from_the_annotators_truth()
    {
        var present = new AnnotatedField { Status = "present", Box = new EvidenceBox(100, 100, 200, 40) };
        var absent = new AnnotatedField { Status = "absent" };

        Assert.Equal(Outcome.Hit, Classify(present, new EvidenceBox(105, 100, 200, 40)).Outcome);
        Assert.Equal(Outcome.Misplaced, Classify(present, new EvidenceBox(100, 700, 200, 40)).Outcome);
        Assert.Equal(Outcome.Missed, Classify(present, null).Outcome);
        Assert.Equal(Outcome.CorrectAbstain, Classify(absent, null).Outcome);
        Assert.Equal(Outcome.FalseOnAbsent, Classify(absent, new EvidenceBox(1, 1, 5, 5)).Outcome);
    }

    [Fact]
    public void Metrics_and_gate_flag_any_misplaced_total()
    {
        static ImageResult Image(Outcome total) => new("x.jpg", false, null, Fields.ToDictionary(field => field,
            field => new FieldResult(field == EvidenceRegionMatcher.TotalField ? total : Outcome.Hit,
                field == EvidenceRegionMatcher.TotalField && total != Outcome.Hit ? null : 0.8, null)));
        var results = Enumerable.Range(0, 29).Select(_ => Image(Outcome.Hit))
            .Append(Image(Outcome.Misplaced))
            .Concat(Enumerable.Range(0, 5).Select(_ => Image(Outcome.CorrectAbstain)))
            .Append(new ImageResult("rotated.jpg", true, "EXIF", new Dictionary<string, FieldResult>()))
            .ToList();

        var report = Evaluate(results);
        var total = Assert.Single(report.Fields, field => field.Field == EvidenceRegionMatcher.TotalField);

        Assert.Equal(36, report.Images);
        Assert.Equal(1, report.Skipped);
        Assert.Equal(30, total.Present);
        Assert.Equal(29, total.Hits);
        Assert.Equal(1, total.Misplaced);
        Assert.Equal(5, total.CorrectAbstain);
        Assert.Equal(29 / 30.0, total.Recall!.Value, 6);
        Assert.Equal(1 / 30.0, total.FalseHighlightRate!.Value, 6);
        var failures = GateFailures(report);
        Assert.Contains(failures, failure => failure.StartsWith("totalAmount có 1 khung đặt nhầm", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("tỉ lệ khoanh nhầm", StringComparison.Ordinal));
        Assert.Contains("CHƯA ĐẠT", ToMarkdown(report, new string('A', 64), "test", failures));
    }

    [Fact]
    public void Gate_requires_enough_images_and_negative_controls()
    {
        var single = Evaluate(new[]
        {
            new ImageResult("a.jpg", false, null, Fields.ToDictionary(field => field, _ => new FieldResult(Outcome.Hit, 0.9, null)))
        });
        var failures = GateFailures(single);
        Assert.Contains(failures, failure => failure.Contains("cần ≥ 30", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("đối chứng âm", StringComparison.Ordinal));
    }

    [Fact]
    public void Annotation_files_are_validated_before_any_ocr_runs()
    {
        var file = new AnnotationFile
        {
            Format = "wrong",
            Images =
            {
                new AnnotatedImage
                {
                    File = "../escape.jpg", Sha256 = "abc",
                    Fields =
                    {
                        [EvidenceRegionMatcher.TotalField] = new AnnotatedField
                        {
                            Status = "present", Box = new EvidenceBox(900, 0, 200, 10)
                        },
                        // The annotation tool exports unanswered fields as "unset"; they must not pass.
                        [EvidenceRegionMatcher.DateField] = new AnnotatedField { Status = "unset" }
                    }
                }
            }
        };

        var problems = Validate(file);

        Assert.Contains(problems, problem => problem.StartsWith("format", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("Tên file", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("SHA-256", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("khung không hợp lệ", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("chưa đánh dấu merchantName", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("documentDate: status sai", StringComparison.Ordinal));
    }

    [Fact]
    public void Annotation_json_from_the_tool_round_trips()
    {
        const string json = """
            {
              "format": "aura-evidence-annotations-v1",
              "annotator": "reviewer-B",
              "images": [{
                "file": "a.jpg", "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
                "width": 1000, "height": 1400,
                "facts": { "merchantName": "CỬA HÀNG A", "identifier": "RCP-1", "identifierField": "receiptNumber",
                           "dateEvidence": "05/10/2026", "totalAmount": 185000, "totalEvidence": "TỔNG THANH TOÁN 185.000 đ" },
                "fields": {
                  "merchantName": { "status": "present", "box": { "x": 10, "y": 20, "width": 300, "height": 30 } },
                  "identifier": { "status": "present", "box": { "x": 10, "y": 60, "width": 300, "height": 20 } },
                  "documentDate": { "status": "absent" },
                  "totalAmount": { "status": "present", "box": { "x": 10, "y": 800, "width": 900, "height": 40 } }
                }
              }]
            }
            """;
        var file = JsonSerializer.Deserialize<AnnotationFile>(json, EvidenceLocalizationEvaluation.Json)!;

        Assert.Empty(Validate(file));
        var facts = ToFacts(file.Images[0].Facts);
        Assert.Equal("RCP-1", facts.ReceiptNumber);
        Assert.Null(facts.InvoiceNumber);
        Assert.Equal(185000m, facts.TotalAmount);
        Assert.False(file.Images[0].Fields["documentDate"].IsPresent);
    }

    /// <summary>
    /// Opt-in locked run: AURA_RUN_LOCALIZATION_EVAL=1, AURA_LOCALIZATION_SET=&lt;folder with
    /// annotations.json and its images&gt;, AURA_TESSDATA_PATH=&lt;tessdata folder&gt;. Writes report.md/json
    /// under test_kit/results (Git-ignored). The test fails when the proposed gate is not met,
    /// unless AURA_LOCALIZATION_GATE=report-only.
    /// </summary>
    [Fact]
    public async Task Locked_annotation_set_is_measured_against_real_ocr()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALIZATION_EVAL") != "1") return;
        var setFolder = Environment.GetEnvironmentVariable("AURA_LOCALIZATION_SET");
        var tessdata = Environment.GetEnvironmentVariable("AURA_TESSDATA_PATH");
        Assert.False(string.IsNullOrWhiteSpace(setFolder), "Set AURA_LOCALIZATION_SET.");
        Assert.False(string.IsNullOrWhiteSpace(tessdata), "Set AURA_TESSDATA_PATH.");

        var annotationPath = Path.Combine(setFolder!, "annotations.json");
        var annotationBytes = await File.ReadAllBytesAsync(annotationPath);
        var annotationSha256 = Convert.ToHexString(SHA256.HashData(annotationBytes));
        var file = JsonSerializer.Deserialize<AnnotationFile>(annotationBytes, EvidenceLocalizationEvaluation.Json)!;
        var problems = Validate(file);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        using var engine = new TesseractReceiptOcrEngine(MsOptions.Create(new EvidenceLocalizationOptions
        {
            Enabled = true, TessdataPath = tessdata!, Languages = "vie+eng", TimeoutSeconds = 60
        }));
        var localizer = $"{engine.Name}/{EvidenceRegionMatcher.Version}";
        var results = new List<ImageResult>();
        foreach (var image in file.Images)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(setFolder!, image.File));
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), image.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{image.File}: bytes differ from the annotated SHA-256.");
            if (ImageOrientation.Read(bytes) != ImageOrientation.Upright)
            {
                results.Add(new ImageResult(image.File, true, "EXIF rotation", new Dictionary<string, FieldResult>()));
                continue;
            }
            var page = await engine.RecognizeAsync(bytes, CancellationToken.None);
            var (regions, _) = EvidenceRegionMatcher.Match(page, ToFacts(image.Facts));
            results.Add(new ImageResult(image.File, false, null, Fields.ToDictionary(field => field, field =>
            {
                var predicted = regions.FirstOrDefault(region => region.Field == field)?.Box;
                var (outcome, iou) = Classify(image.Fields[field], predicted);
                return new FieldResult(outcome, iou, predicted);
            })));
        }

        var report = Evaluate(results);
        var failures = GateFailures(report);
        var markdown = ToMarkdown(report, annotationSha256, localizer, failures);
        var outputFolder = Path.Combine(ProjectRoot, "test_kit", "results",
            "localization-eval-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(outputFolder);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "report.md"), markdown);
        await File.WriteAllTextAsync(Path.Combine(outputFolder, "report.json"), JsonSerializer.Serialize(new
        {
            annotationSha256, localizer, iouThreshold = IouThreshold, gatePassed = failures.Count == 0,
            gateFailures = failures, report
        }, EvidenceLocalizationEvaluation.Json));
        output.WriteLine(markdown);
        output.WriteLine("Report: " + outputFolder);

        if (Environment.GetEnvironmentVariable("AURA_LOCALIZATION_GATE") != "report-only")
            Assert.True(failures.Count == 0, "Gate chưa đạt:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AURA.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy AURA.csproj từ thư mục test output.");
    }
}
