using FIS.Web.Components;
using FIS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
