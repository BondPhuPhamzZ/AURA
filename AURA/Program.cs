using AURA.Interfaces;
using AURA.Services;
using AURA.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using AURA.Options;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Keep logging portable across Windows development, containers and cloud hosts.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Đăng ký Services
builder.Services.AddControllersWithViews();
builder.Services.AddOptions<GeminiOptions>()
    .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<ReceiptStorageOptions>()
    .Bind(builder.Configuration.GetSection(ReceiptStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var maxUploadBytes = builder.Configuration.GetValue<int>("ReceiptStorage:MaxFileSizeMb", 5) * 1024L * 1024L;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes);

// Cấu hình Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đăng ký Dependency Injection
builder.Services.AddScoped<IReimbursementRepository, ReimbursementRepository>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddHttpClient<IVisionExtractor, GeminiVisionExtractorService>((services, client) =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    ;

app.Run();






