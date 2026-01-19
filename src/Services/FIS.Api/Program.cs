using System.Text;
using AspNetCoreRateLimit;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Auth;
using FIS.Core.Application.Services;
using FIS.Core.Application.Services.Auth;
using FIS.Core.Application.Services.Billing;
using FIS.Core.Application.Services.Validation;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Core.Infrastructure.Repositories;
using FIS.Core.Infrastructure.Services;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure CORS for network access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNetwork", policy =>
    {
        policy.WithOrigins(
            // Localhost (backward compatibility)
            "http://localhost:5268",
            "https://localhost:7259",
            "http://localhost:5010",
            "https://localhost:7188",
            // Network IP access (10.0.0.104)
            "http://10.0.0.104:5268",
            "https://10.0.0.104:7259",
            "http://10.0.0.104:5010",
            "https://10.0.0.104:7188"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Configure rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure Entity Framework
builder.Services.AddDbContext<FisDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
);

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new()
        {
            Title = "FIS API",
            Version = "v1",
            Description =
                "Fleet Information System - Modern API with Legacy Database Compatibility",
        }
    );

    // Add JWT Bearer authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure multi-scheme authentication (Entra ID + Legacy JWT)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("LegacyJWT", options =>
    {
        // Legacy JWT validation
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Microsoft Entra ID / Azure AD validation
        options.Authority = $"{builder.Configuration["AzureAd:Instance"]}{builder.Configuration["AzureAd:TenantId"]}/v2.0";
        options.Audience = builder.Configuration["AzureAd:ClientId"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

// Configure authorization to accept EITHER authentication scheme
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("LegacyJWT", JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// Register business services
builder.Services.AddScoped<FuelCardManagementService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<IReportingService, ReportingService>(); // Re-enabled with PDF service
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();

// Register PDF service (stub implementation)
builder.Services.AddScoped<IPdfGenerationService, PdfGenerationService>();

// Financial services
builder.Services.AddScoped<ITariffCalculationService, TariffCalculationService>();
builder.Services.AddScoped<IJournalDetailService, JournalDetailService>();

// Business logic services
builder.Services.AddScoped<IContractValidationService, ContractValidationService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();

// Authentication services (Dual auth - Entra ID + Legacy JWT)
builder.Services.AddScoped<IUserClaimsService, UserClaimsService>();

// Register repositories
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<ITripRepository, TripRepository>();

// Phase 1 repositories - Core vehicle management
builder.Services.AddScoped<IMakeRepository, MakeRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<ITypeRepository, TypeRepository>();
builder.Services.AddScoped<IFuelTypeRepository, FuelTypeRepository>();
builder.Services.AddScoped<IMaintenanceTriggerRepository, MaintenanceTriggerRepository>();
builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
builder.Services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
builder.Services.AddScoped<IContractStatusRepository, ContractStatusRepository>();

// Phase 4: Additional entity repositories
builder.Services.AddScoped<ITripDriverRepository, TripDriverRepository>();
builder.Services.AddScoped<IPrivateHireRepository, PrivateHireRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IMaintenanceRecordRepository, MaintenanceRecordRepository>();

// Operations repositories (Fleet management operations)
builder.Services.AddScoped<IAuctionRepository, AuctionRepository>();
builder.Services.AddScoped<ICallCentreRepository, CallCentreRepository>();
builder.Services.AddScoped<IClearanceRepository, ClearanceRepository>();
builder.Services.AddScoped<IFineRepository, FineRepository>();
builder.Services.AddScoped<ILogbookRepository, LogbookRepository>();
builder.Services.AddScoped<ILogsheetRepository, LogsheetRepository>();
builder.Services.AddScoped<ILossRepository, LossRepository>();
builder.Services.AddScoped<IMonitorRepository, MonitorRepository>();
builder.Services.AddScoped<ITaxiRepository, TaxiRepository>();
builder.Services.AddScoped<ITowingRepository, TowingRepository>();
// TripAuthorityRepository removed - conflicts with existing Trip entity
builder.Services.AddScoped<IVehicleOrderRepository, VehicleOrderRepository>();
builder.Services.AddScoped<IVehiclePhotoRepository, VehiclePhotoRepository>();
builder.Services.AddScoped<IWorkshopRepository, WorkshopRepository>();

// Batch 3 repositories (Accident management and vehicle tracking)
builder.Services.AddScoped<IAccidentRepository, AccidentRepository>();
builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();
builder.Services.AddScoped<IVehicleAssessmentRepository, VehicleAssessmentRepository>();
builder.Services.AddScoped<IVehicleDamageRepository, VehicleDamageRepository>();
builder.Services.AddScoped<ITrafficDeptRepository, TrafficDeptRepository>();

// Batch 4 repositories (Asset verification, lease, bookings, suppliers)
builder.Services.AddScoped<IAssetVerificationRepository, AssetVerificationRepository>();
builder.Services.AddScoped<ILeaseContractTermsRepository, LeaseContractTermsRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();

// Authentication repositories (Dual auth - Entra ID + Legacy JWT)
builder.Services.AddScoped<IEntraIdUserMappingRepository, EntraIdUserMappingRepository>();

// Financial system repositories - temporarily disabled for debugging
//builder.Services.AddScoped<ITariffRepository, TariffRepository>();
//builder.Services.AddScoped<IVehicleTariffRepository, VehicleTariffRepository>();
//builder.Services.AddScoped<ILeaseTariffRepository, LeaseTariffRepository>();
//builder.Services.AddScoped<ITariffParameterRepository, TariffParameterRepository>();
//builder.Services.AddScoped<IMaintenanceValueRepository, MaintenanceValueRepository>();
//builder.Services.AddScoped<IOverheadRepository, OverheadRepository>();
//builder.Services.AddScoped<ITariffWeightCalculationRepository, TariffWeightCalculationRepository>();
//builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>(); // TODO: Fix interface namespace issue
//builder.Services.AddScoped<IInvoiceItemRepository, InvoiceItemRepository>(); // TODO: Fix interface namespace issue
builder.Services.AddScoped<IJournalDetailRepository, JournalDetailRepository>();

// Phase 3 complete: All missing API controllers implemented (Department, Driver, User, Trip)

// Add health checks
builder.Services.AddHealthChecks().AddDbContextCheck<FisDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FIS API v1");
        options.RoutePrefix = string.Empty; // Make Swagger UI the default page
    });
}

// Add security headers
app.UseHttpsRedirection();

// Rate limiting middleware (must be before CORS and routing)
app.UseIpRateLimiting();

// Enable CORS
app.UseCors("AllowNetwork");

// Authentication & Authorization middleware
// CRITICAL ORDER: Must be after CORS, before MapControllers
app.UseAuthentication();
app.UseAuthorization();

// Add health check endpoints
app.MapHealthChecks("/health");

// Add controllers
app.MapControllers();

app.Run();
