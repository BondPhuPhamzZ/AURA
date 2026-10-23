using AURA.Interfaces;
using AURA.Services;
using AURA.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AURA.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Keep logging portable across Windows development, containers and cloud hosts.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
// The built-in HttpClient logs include Drive file/folder IDs in request URLs.
builder.Logging.AddFilter("System.Net.Http.HttpClient.DriveSandboxClient", LogLevel.Warning);

// Đăng ký Services
builder.Services.AddControllersWithViews();
builder.Services.AddRateLimiter(options =>
{
    // A conservative, process-local admission guard for the anonymous demo surface.
    // Production capacity and abuse protection still require identity and an edge limiter.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"Hệ thống đang nhận quá nhiều yêu cầu. Vui lòng thử lại sau.\"}", cancellationToken);
    };
    options.AddFixedWindowLimiter("receipt-intake", limiter =>
    {
        limiter.PermitLimit = 15;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("verify-harness", limiter =>
    {
        limiter.PermitLimit = 1;
        limiter.Window = TimeSpan.FromMinutes(2);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddOptions<VisionOptions>()
    .Bind(builder.Configuration.GetSection(VisionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => VisionOptions.IsSupportedProvider(options.Provider),
        "Vision:Provider phải là OpenRouter hoặc Ollama.")
    .Validate(options => VisionOptions.IsSupportedProvider(options.FallbackProvider),
        "Vision:FallbackProvider phải là OpenRouter hoặc Ollama.")
    .Validate(options => !options.FallbackEnabled ||
        !string.Equals(options.Provider, options.FallbackProvider, StringComparison.OrdinalIgnoreCase),
        "Vision:FallbackProvider phải khác Vision:Provider khi bật fallback.")
    .Validate(options => !options.UseSqlDistributedGate || options.MaxConcurrentRequests == 1,
        "Vision:UseSqlDistributedGate hiện chỉ hỗ trợ một AI permit toàn hệ thống.")
    .ValidateOnStart();
builder.Services.AddOptions<OpenRouterOptions>()
    .Bind(builder.Configuration.GetSection(OpenRouterOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps, "OpenRouter:BaseUrl phải là HTTPS URL hợp lệ.")
    .Validate(options => Uri.TryCreate(options.HttpReferer, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps, "OpenRouter:HttpReferer phải là HTTPS URL hợp lệ.")
    .Validate(options => !Uri.TryCreate(options.ChatCompletionsPath, UriKind.Absolute, out _)
        && !options.ChatCompletionsPath.StartsWith('/'),
        "OpenRouter:ChatCompletionsPath phải là đường dẫn tương đối, ví dụ chat/completions.")
    .Validate(options => options.Model.StartsWith("qwen/", StringComparison.OrdinalIgnoreCase),
        "OpenRouter:Model phải là model Qwen trên OpenRouter.")
    .Validate(options => OpenRouterOptions.IsSupportedReasoningEffort(options.ReasoningEffort),
        "OpenRouter:ReasoningEffort phải để trống hoặc là none, minimal, low, medium, high, xhigh, max.")
    .ValidateOnStart();
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
         (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))),
        "Ollama:BaseUrl chỉ cho phép HTTPS hoặc HTTP loopback local.")
    .Validate(options => !Uri.TryCreate(options.ChatPath, UriKind.Absolute, out _) &&
        !options.ChatPath.StartsWith('/'),
        "Ollama:ChatPath phải là đường dẫn tương đối, ví dụ api/chat.")
    .Validate(options => options.Model.StartsWith("qwen3-vl", StringComparison.OrdinalIgnoreCase),
        "Ollama:Model phải là model Qwen3-VL local.")
    .ValidateOnStart();
builder.Services.AddOptions<ReceiptStorageOptions>()
    .Bind(builder.Configuration.GetSection(ReceiptStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<DecisionPolicyOptions>()
    .Bind(builder.Configuration.GetSection(DecisionPolicyOptions.SectionName));
builder.Services.AddOptions<ReceiptProcessingOptions>()
    .Bind(builder.Configuration.GetSection(ReceiptProcessingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<VerifyOptions>()
    .Bind(builder.Configuration.GetSection(VerifyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<DriveSandboxOptions>()
    .Bind(builder.Configuration.GetSection(DriveSandboxOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
    {
        if (!options.Enabled || !builder.Environment.IsDevelopment()) return true;
        if (string.IsNullOrWhiteSpace(options.FolderId) ||
            !options.FolderId.All(character => char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_') ||
            !Path.IsPathFullyQualified(options.ServiceAccountJsonPath) ||
            !File.Exists(options.ServiceAccountJsonPath))
            return false;
        var relative = Path.GetRelativePath(builder.Environment.ContentRootPath,
            Path.GetFullPath(options.ServiceAccountJsonPath));
        return Path.IsPathFullyQualified(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    },
        "DriveSandbox local cần folder ID hợp lệ và service-account JSON nằm ngoài project.")
    .ValidateOnStart();

var maxUploadBytes = builder.Configuration.GetValue<int>("ReceiptStorage:MaxFileSizeMb", 5) * 1024L * 1024L;
// Multipart contains the file plus antiforgery fields, boundaries and headers. Keep transport
// headroom here; ApplicantController remains the authority for the actual file-size limit.
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes + 1024L * 1024L);

// Cấu hình Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Dependency Injection
builder.Services.AddScoped<IReimbursementRepository, ReimbursementRepository>();
builder.Services.AddScoped<ReceiptIntakeService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<WorkflowOperationGate>();
builder.Services.AddSingleton<ReceiptProcessingQueue>();
builder.Services.AddSingleton<VisionCircuitBreaker>();
builder.Services.AddSingleton<VisionInvocationCoordinator>();
builder.Services.AddScoped<VisionExecutionContext>();
builder.Services.AddHostedService<ReceiptProcessingWorker>();
builder.Services.AddHttpClient<DriveSandboxClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });
builder.Services.AddHostedService<DriveSandboxWorker>();
builder.Services.AddHttpClient<OpenRouterVisionExtractorService>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenRouterOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHttpClient<OllamaVisionExtractorService>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<IVisionExtractor, ConfiguredVisionExtractor>();

var app = builder.Build();

var configuredStorage = app.Services.GetRequiredService<IOptions<ReceiptStorageOptions>>().Value;
var receiptStorageRoot = ReceiptStoragePath.ResolveRoot(app.Environment, configuredStorage.Directory);
Directory.CreateDirectory(receiptStorageRoot);

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
}

// Cấu hình HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/healthz", async (IOptions<VisionOptions> configuredVision,
    IOptions<OpenRouterOptions> configuredOpenRouter,
    IOptions<OllamaOptions> configuredOllama,
    IOptions<VerifyOptions> configuredVerify,
    IOptions<DriveSandboxOptions> configuredDriveSandbox,
    IWebHostEnvironment environment,
    AppDbContext database,
    CancellationToken cancellationToken) =>
{
    var vision = configuredVision.Value;
    var openRouter = configuredOpenRouter.Value;
    var ollama = configuredOllama.Value;
    var useOllama = string.Equals(vision.Provider, VisionOptions.OllamaProvider,
        StringComparison.OrdinalIgnoreCase);
    var policyPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath,
        useOllama ? ollama.PolicyPath : openRouter.PolicyPath));
    var policyAvailable = File.Exists(policyPath);
    var aiConfigured = useOllama || !string.IsNullOrWhiteSpace(openRouter.ApiKey);
    bool databaseAvailable;
    var pendingMigrationCount = -1;
    try
    {
        databaseAvailable = await database.Database.CanConnectAsync(cancellationToken);
        if (databaseAvailable)
        {
            pendingMigrationCount = (await database.Database
                .GetPendingMigrationsAsync(cancellationToken)).Count();
        }
    }
    catch
    {
        databaseAvailable = false;
    }

    var storageAvailable = Directory.Exists(receiptStorageRoot);
    var databaseUpToDate = databaseAvailable && pendingMigrationCount == 0;
    var ready = policyAvailable && aiConfigured && databaseUpToDate && storageAvailable;

    return Results.Json(new
    {
        status = ready ? "ok" : "degraded",
        service = "AURA",
        aiConfigured,
        policyAvailable,
        databaseAvailable,
        databaseUpToDate,
        pendingMigrationCount = pendingMigrationCount < 0 ? (int?)null : pendingMigrationCount,
        storageAvailable,
        provider = useOllama ? VisionOptions.OllamaProvider : VisionOptions.OpenRouterProvider,
        model = useOllama ? ollama.Model : openRouter.Model,
        reasoningEffort = useOllama ? null : openRouter.ReasoningEffort,
        providerDataCollectionDenied = useOllama ? (bool?)null : openRouter.DenyProviderDataCollection,
        strictZeroDataRetention = useOllama ? (bool?)null : openRouter.RequireZeroDataRetention,
        processingMode = "durable-background-queue",
        maxConcurrentAiRequests = vision.MaxConcurrentRequests,
        sqlDistributedAiGateEnabled = vision.UseSqlDistributedGate,
        providerCooldownSeconds = vision.ProviderCooldownSeconds,
        verifyHarnessEnabled = VerifyExecutionPolicy.IsAllowed(
            configuredVerify.Value.Enabled, environment.EnvironmentName),
        driveSandboxConfigured = DriveSandboxRuntimePolicy.IsAllowed(configuredDriveSandbox.Value,
            environment.EnvironmentName, builder.Configuration["urls"]),
        fallbackEnabled = vision.FallbackEnabled,
        configuredFallbackProvider = vision.FallbackProvider,
        fallbackProvider = vision.FallbackEnabled ? vision.FallbackProvider : null,
        timestamp = DateTimeOffset.UtcNow
    }, statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    ;

app.Run();








