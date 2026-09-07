using FIS.Web.Components;
using FIS.Web.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);
var dotEnvRawValues = Env.NoEnvVars().TraversePath().Load();
var dotEnvValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
var dotEnvConfig = new Dictionary<string, string?>();

foreach (var pair in dotEnvRawValues)
{
    dotEnvValues[pair.Key] = pair.Value;
}

// Map every KEY__SUBKEY from .env into KEY:SUBKEY for ASP.NET Core configuration
foreach (var pair in dotEnvValues)
{
    dotEnvConfig[pair.Key.Replace("__", ":")] = pair.Value;
}

builder.Configuration.AddInMemoryCollection(dotEnvConfig);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var isMicrosoftIdentityConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]);

// Configure Microsoft Entra ID authentication
if (isMicrosoftIdentityConfigured)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(options =>
        {
            builder.Configuration.Bind("AzureAd", options);

            // Configure logout behavior
            options.Events = new OpenIdConnectEvents
            {
                OnSignedOutCallbackRedirect = context =>
                {
                    // Redirect to signed out page after Microsoft completes logout
                    context.Response.Redirect("/signedout");
                    context.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });

    // Add controllers with views for Microsoft Identity UI (login/logout pages)
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
}
else
{
    builder.Services.AddAuthentication();

    // MVC controllers are still needed for legacy auth proxy endpoints.
    builder.Services.AddControllersWithViews();
}

builder.Services.AddAuthorization();

// Add session support (required for stable TokenService circuit identification)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Match JWT token expiry (480 minutes)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for GDPR compliance
    options.Cookie.Name = ".FIS.Session";
    // SameAsRequest: cookie is Secure-only when served over HTTPS, plain when served over HTTP.
    // Once nginx terminates TLS, X-Forwarded-Proto + UseForwardedHeaders make this Secure automatically.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Honor X-Forwarded-Proto from nginx so Request.IsHttps is correct behind the reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Register HttpContextAccessor for dual auth
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuthSessionTokenCache>();
builder.Services.AddTransient<SessionCookieAuthHandler>();
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddHttpMessageHandler<SessionCookieAuthHandler>();
});

// Register TokenService (scoped to user circuit) — used for refresh-token rotation, NOT auth state
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<SidebarStateService>();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5010";
var apiBaseUri = new Uri($"{apiBaseUrl.TrimEnd('/')}/");

// Session-based authentication state provider — single source of truth.
// Calls /api/auth/validate to get the full claim set (including access_level + role claims).
builder.Services.AddHttpClient(nameof(SessionAuthenticationStateProvider), client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<SessionAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<SessionAuthenticationStateProvider>());

// Register API services
builder.Services.AddHttpClient<VehicleApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<VehicleDocumentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ReferenceDataApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<DepartmentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<DriverApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ContractApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<PrivateHireApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<PrivateHireFuelCardApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<AuditApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<AccidentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<FleetManagementApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<LicenseApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LicenseMaintenanceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LicenseFeeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<DriverLicenceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ExtraCodeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LossTypeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ProvinceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<MaintenanceRecordApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<MaintenanceTriggerApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<NotificationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TroubleshootApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ReportCatalogApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ReportApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<CaptureActivityApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TariffApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<FinanceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TripApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TripDriverApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<UnitOfMeasureApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<UserApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<UserProfileApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<UserAccessContextService>();
builder.Services.AddHttpClient<AuthApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SiteApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<CallCentreApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<NotifyListApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ClassApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<RegistrationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LogbookApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LogsheetApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<MonitorApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TrackingApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<AuctionApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<MerchantApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LicenseReportApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleLookupApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<WorkshopApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TaxiApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TaxiLogApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<AssetVerificationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleAssessmentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleDamageApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SupplierApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ThirdPartyApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LeaseContractTermsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<JobCardApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ValidationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// MUST come first: respect X-Forwarded-Proto from nginx so the rest of the pipeline knows the request was HTTPS
app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseStaticFiles();
var sharedDocsPath = Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "Docs");
if (Directory.Exists(sharedDocsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(sharedDocsPath),
        RequestPath = "/Docs"
    });
}

// Session middleware (must come before authentication)
app.UseSession();
app.UseAntiforgery();

// Prime circuit/request auth token state from cookie/session early in the pipeline.
app.Use(async (context, next) =>
{
    var tokenService = context.RequestServices.GetService<TokenService>();
    if (tokenService != null)
    {
        await tokenService.InitializeAsync();
    }

    await next();
});

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map Microsoft Identity UI controllers (for login/logout)
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
