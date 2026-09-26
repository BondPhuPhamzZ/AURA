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

var builder = WebApplication.CreateBuilder(args);

// Keep logging portable across Windows development, containers and cloud hosts.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Đăng ký Services
builder.Services.AddControllersWithViews();
builder.Services.AddOptions<VisionOptions>()
    .Bind(builder.Configuration.GetSection(VisionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => VisionOptions.IsSupportedProvider(options.Provider),
        "Vision:Provider phải là OpenRouter hoặc Ollama.")
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

var maxUploadBytes = builder.Configuration.GetValue<int>("ReceiptStorage:MaxFileSizeMb", 5) * 1024L * 1024L;
// Multipart contains the file plus antiforgery fields, boundaries and headers. Keep transport
// headroom here; ApplicantController remains the authority for the actual file-size limit.
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes + 1024L * 1024L);

// Cấu hình Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Dependency Injection
builder.Services.AddScoped<IReimbursementRepository, ReimbursementRepository>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddSingleton<WorkflowOperationGate>();
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
app.UseAuthorization();

app.MapGet("/healthz", (IOptions<VisionOptions> configuredVision,
    IOptions<OpenRouterOptions> configuredOpenRouter,
    IOptions<OllamaOptions> configuredOllama,
    IWebHostEnvironment environment) =>
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
    var ready = policyAvailable && aiConfigured;

    return Results.Json(new
    {
        status = ready ? "ok" : "degraded",
        service = "AURA",
        aiConfigured,
        policyAvailable,
        provider = useOllama ? VisionOptions.OllamaProvider : VisionOptions.OpenRouterProvider,
        model = useOllama ? ollama.Model : openRouter.Model,
        timestamp = DateTimeOffset.UtcNow
    }, statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    ;

app.Run();








