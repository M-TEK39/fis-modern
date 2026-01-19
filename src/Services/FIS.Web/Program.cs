using FIS.Web.Components;
using FIS.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Microsoft Entra ID authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// Add controllers with views for Microsoft Identity UI (login/logout pages)
builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Register API services
builder.Services.AddHttpClient<VehicleApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ReferenceDataApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<DepartmentApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<DriverApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ContractApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<PrivateHireApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<AccidentApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<FleetManagementApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<LicenseApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<LocationApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<MaintenanceRecordApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<MaintenanceTriggerApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<NotificationApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<ReportCatalogApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TariffApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TripApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<TripDriverApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<UnitOfMeasureApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<UserApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SiteApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<CallCentreApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<FineApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LogbookApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LogsheetApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<MonitorApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TrackingApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TowingApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<AuctionApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ClearanceApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LossApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleOrderApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehiclePhotoApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<WorkshopApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TaxiApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<AssetVerificationApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleAssessmentApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<VehicleDamageApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<SupplierApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<BookingApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<LeaseContractTermsApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<TrafficDeptApiService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5010/");
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

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map Microsoft Identity UI controllers (for login/logout)
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
