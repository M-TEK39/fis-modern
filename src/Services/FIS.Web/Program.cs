using FIS.Web.Components;
using FIS.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Microsoft Entra ID authentication
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

builder.Services.AddAuthorization();

// Add controllers with views for Microsoft Identity UI (login/logout pages)
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Add session support (required for stable TokenService circuit identification)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Match JWT token expiry (480 minutes)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for GDPR compliance
    options.Cookie.Name = ".FIS.Session";
});

// Register HttpContextAccessor for dual auth
builder.Services.AddHttpContextAccessor();

// Register TokenService (scoped to user circuit)
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<SidebarStateService>();

// Register AuthorizationHeaderHandler (adds JWT to API requests)
// Transient because it resolves TokenService dynamically from IServiceProvider
builder.Services.AddTransient<AuthorizationHeaderHandler>();

// Register JWT AuthenticationStateProvider for Blazor Server
builder.Services.AddScoped<JwtAuthenticationStateProvider>();

// Register DualAuthStateProvider as both itself AND as AuthenticationStateProvider
builder.Services.AddScoped<DualAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => 
    sp.GetRequiredService<DualAuthStateProvider>());

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5010";
var webBaseUrl = builder.Configuration["ApiSettings:WebBaseUrl"] ?? "http://localhost:5268";
var apiBaseUri = new Uri($"{apiBaseUrl.TrimEnd('/')}/");
var webBaseUri = new Uri($"{webBaseUrl.TrimEnd('/')}/");

// Register API services with AuthorizationHeaderHandler (automatically adds JWT to requests)
builder.Services.AddHttpClient<VehicleApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<VehicleDocumentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<ReferenceDataApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<DepartmentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<DriverApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<ContractApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<PrivateHireApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<AuditApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<AccidentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<FleetManagementApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<LicenseApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LicenseMaintenanceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LicenseFeeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<DriverLicenceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ExtraCodeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LossTypeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ProvinceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<LocationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<MaintenanceRecordApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<MaintenanceTriggerApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<NotificationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<TroubleshootApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<NoticeApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<ReportCatalogApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<ReportApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<CaptureActivityApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<TariffApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<FinanceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<TripApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<TripDriverApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<UnitOfMeasureApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

builder.Services.AddHttpClient<UserApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<UserProfileApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddScoped<UserAccessContextService>();
builder.Services.AddHttpClient<AuthApiService>(client =>
{
    // Point to local web server (AuthProxyController), NOT the API
    // No AuthorizationHeaderHandler needed - this calls local proxy, not API
    client.BaseAddress = webBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SiteApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<CallCentreApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ClassApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<FineApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<RegistrationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LogbookApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LogsheetApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<MonitorApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<TrackingApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<TowingApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<AuctionApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ClearanceApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<MerchantApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LossApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LossReportApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LicenseReportApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<VehicleOrderApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<VehiclePhotoApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<VehicleLookupApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<WorkshopApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<TaxiApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<TaxiLogApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<AssetVerificationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<VehicleAssessmentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<VehicleDamageApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<SupplierApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ThirdPartyApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<BookingApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<LeaseContractTermsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<JobCardApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<TrafficDeptApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();
builder.Services.AddHttpClient<ValidationApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<AuthorizationHeaderHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Session middleware (must come before authentication)
app.UseSession();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map Microsoft Identity UI controllers (for login/logout)
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
