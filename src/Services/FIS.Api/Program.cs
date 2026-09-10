using AspNetCoreRateLimit;
using DotNetEnv;
using FIS.Api.Services;
using FIS.Api.Services.SessionManagement;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Auth;
using FIS.Core.Application.Interfaces.Repositories;
using FIS.Core.Application.Interfaces.SystemConfiguration;
using FIS.Core.Application.Services;
using FIS.Core.Application.Services.Auth;
using FIS.Core.Application.Services.Billing;
using FIS.Core.Application.Services.Validation;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Core.Infrastructure.Repositories;
using FIS.Core.Infrastructure.Services;
using FIS.Core.Infrastructure.Services.EmailDelivery;
using FIS.Core.Infrastructure.Services.SystemConfiguration;
using FIS.Data.SqlServer;
using FIS.Data.SqlServer.Compatibility;
using FIS.Data.SqlServer.Interceptors;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

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

// Shell and container environment must override the optional local .env file.
// This lets host-local development target Docker SQL Server without changing
// tracked application settings, while preserving .env as a fallback only.
builder.Configuration.AddEnvironmentVariables();

// Key Vault can safely override only the system security settings allow-listed
// by SystemConfigurationService. It runs before the authentication and
// session services are composed, while deployment configuration remains the
// fallback when the vault is unavailable.
SystemConfigurationService.ApplyStartupOverrides(builder.Configuration);

// Add services to the container.
builder.Services.AddControllers();

// Configure CORS for network access
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowNetwork",
        policy =>
        {
            policy
                .WithOrigins(
                    // Localhost (backward compatibility)
                    "http://localhost:5268",
                    "https://localhost:7259",
                    "http://localhost:5010",
                    "https://localhost:7188",
                    // Network IP access (10.0.0.104)
                    "http://10.0.0.104:5268",
                    "https://10.0.0.104:7259",
                    "http://10.0.0.104:5010",
                    "https://10.0.0.104:7188",
                    // Production domains via Cloudflare Tunnel
                    "https://fis.irisgroup.co.za",
                    "https://admin.irisgroup.co.za"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    );
});

// Configure rate limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Audit trail infrastructure
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditInterceptor>();

// Configure Entity Framework
var connectionString = SqlServerConnectionStringHelper.Resolve(
    builder.Configuration.GetConnectionString("Default"),
    builder.Environment.IsDevelopment()
);
builder.Services.AddDbContext<FisDbContext>(
    (serviceProvider, options) =>
    {
        options.UseSqlServer(connectionString);
        options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
    }
);

// Configure Hangfire for background jobs (Phase 5 - Analytics)
builder.Services.AddHangfire(configuration =>
    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            connectionString,
            new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
            }
        )
);

builder.Services.AddHangfireServer();

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

    // Add cookie authentication note to Swagger
    options.AddSecurityDefinition(
        "CookieAuth",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description =
                "Session authentication uses HttpOnly cookies FIS_Access_Token and FIS_Refresh_Token.",
            Name = "Cookie",
            In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        }
    );

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "CookieAuth",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

builder.Services.AddSingleton<ISessionTokenStore, SqlSessionTokenStore>();
builder.Services.AddScoped<ISessionManagementService, SqlSessionManagementService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var authenticationBuilder = builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = SessionCookieAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = SessionCookieAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, SessionCookieAuthenticationHandler>(
        SessionCookieAuthenticationHandler.SchemeName,
        _ => { }
    );

if (
    builder.Configuration.GetValue<bool?>("SystemSettings:EntraEnabled") != false
    &&
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:TenantId"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientSecret"])
)
{
    authenticationBuilder.AddMicrosoftIdentityWebApp(
        options =>
        {
            builder.Configuration.Bind("AzureAd", options);
            options.CallbackPath = MicrosoftAuthenticationDefaults.CallbackPath;
        },
        cookieOptions =>
        {
            cookieOptions.Cookie.Name = ".FIS.MicrosoftIdentity";
            cookieOptions.Cookie.HttpOnly = true;
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        },
        openIdConnectScheme: MicrosoftAuthenticationDefaults.OpenIdConnectScheme,
        cookieScheme: MicrosoftAuthenticationDefaults.CookieScheme
    );
}

builder.Services.AddAuthorization();

// Register business services
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContextService>();
builder.Services.AddScoped<FuelCardManagementService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<IReportingService, ReportingService>(); // Re-enabled with PDF service
builder.Services.AddScoped<ILegacyReportResultService, LegacyReportResultService>();
builder.Services.AddSingleton<EmailDeliveryConfigurationStore>();
builder.Services.AddSingleton<IEmailDeliveryProvider, GraphEmailDeliveryProvider>();
builder.Services.AddSingleton<IEmailDeliveryProvider, SmtpEmailDeliveryProvider>();
builder.Services.AddSingleton<IEmailDeliveryProvider, SendGridEmailDeliveryProvider>();
builder.Services.AddSingleton<EmailDeliveryService>();
builder.Services.AddSingleton<FIS.Core.Application.Interfaces.EmailDelivery.IEmailDeliveryService>(
    serviceProvider => serviceProvider.GetRequiredService<EmailDeliveryService>()
);
builder.Services.AddSingleton<FIS.Core.Application.Interfaces.EmailDelivery.IEmailConfigurationService>(
    serviceProvider => serviceProvider.GetRequiredService<EmailDeliveryService>()
);
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddSingleton<ISystemConfigurationAuditSink, SystemConfigurationAuditLogger>();
builder.Services.AddSingleton<ISystemConfigurationService, SystemConfigurationService>();
builder.Services.AddScoped<LegacyCredentialCompatibilityService>();
builder.Services.AddScoped<LegacyUserProfileOptionalFieldsService>();
builder.Services.AddScoped<MicrosoftIdentityCompatibilityService>();
builder.Services.AddScoped<NotifyListCompatibilityService>();
builder.Services.AddScoped<TowTruckCompatibilityService>();
builder.Services.AddScoped<AccidentCompatibilityService>();
builder.Services.AddScoped<LossCompatibilityService>();
builder.Services.AddScoped<CallCentreEditCompatibilityService>();

// Register lightweight PDF service.
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
builder.Services.AddScoped<IGgBlockRepository, GgBlockRepository>();
builder.Services.AddScoped<IVehicleSourceRepository, VehicleSourceRepository>();
builder.Services.AddScoped<IVehicleStatusReportRepository, VehicleStatusReportRepository>();
builder.Services.AddScoped<IFmlReportRepository, FmlReportRepository>();
builder.Services.AddScoped<IRecoveredVehicleRepository, RecoveredVehicleRepository>();
builder.Services.AddScoped<IDemoVehicleRepository, DemoVehicleRepository>();
builder.Services.AddScoped<IVehicleAuthorizationRepository, VehicleAuthorizationRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IContractAuditLogRepository, ContractAuditLogRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IAccessLevelRepository, AccessLevelRepository>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<ITripRepository, TripRepository>();

// Phase 1 repositories - Core vehicle management
builder.Services.AddScoped<IMakeRepository, MakeRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<ITypeRepository, TypeRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IFuelTypeRepository, FuelTypeRepository>();
builder.Services.AddScoped<IFuelTariffRepository, FuelTariffRepository>();
builder.Services.AddScoped<IJobCardRepository, JobCardRepository>();
builder.Services.AddScoped<IVehicleRemarkRepository, VehicleRemarkRepository>();
builder.Services.AddScoped<IVehicleLicenceHistoryRepository, VehicleLicenceHistoryRepository>();
builder.Services.AddScoped<IVehicleDocumentRepository, VehicleDocumentRepository>();
builder.Services.AddScoped<ILicenseCertificateRepository, LicenseCertificateRepository>();
builder.Services.AddScoped<ITaxiScanDocRepository, TaxiScanDocRepository>();
builder.Services.AddScoped<IMaintenanceTriggerRepository, MaintenanceTriggerRepository>();
builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
builder.Services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
builder.Services.AddScoped<IContractStatusRepository, ContractStatusRepository>();

// Reference Data repositories (Priority 1 - Quick wins for Codex)
builder.Services.AddScoped<IDriverLicenceRepository, DriverLicenceRepository>();
builder.Services.AddScoped<ILicenseFeeRepository, LicenseFeeRepository>();
builder.Services.AddScoped<IExtraCodeRepository, ExtraCodeRepository>();
builder.Services.AddScoped<ILossTypeRepository, LossTypeRepository>();

// Notice Management System repositories (Priority 3)
builder.Services.AddScoped<INoticeRepository, NoticeRepository>();
builder.Services.AddScoped<INoticeScheduleRepository, NoticeScheduleRepository>();

// Third Party / Supplier Management repositories (Priority 5)
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IClassRequirementRepository, ClassRequirementRepository>();
builder.Services.AddScoped<IThirdPartyRentalRepository, ThirdPartyRentalRepository>();

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
builder.Services.AddScoped<ITaxiLogRepository, TaxiLogRepository>();
builder.Services.AddScoped<ITaxiLogNoteRepository, TaxiLogNoteRepository>();
builder.Services.AddScoped<ITaxiWhiteLogRepository, TaxiWhiteLogRepository>();
builder.Services.AddScoped<IContractorTaxiClassRepository, ContractorTaxiClassRepository>();
builder.Services.AddScoped<ITowingRepository, TowingRepository>();

// TripAuthorityRepository removed - conflicts with existing Trip entity
builder.Services.AddScoped<IVehicleOrderRepository, VehicleOrderRepository>();
builder.Services.AddScoped<IVehiclePhotoRepository, VehiclePhotoRepository>();
builder.Services.AddScoped<IWorkshopRepository, WorkshopRepository>();
builder.Services.AddScoped<IWorkshopMerchantRepository, WorkshopMerchantRepository>();

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

// Phase 3 repositories (Administrative endpoints - Fuel Cards, Notice Management, Reference Data)
builder.Services.AddScoped<IFuelCardRepository, FuelCardRepository>();
builder.Services.AddScoped<IPrivateHireFuelCardRepository, PrivateHireFuelCardRepository>();

// Workflow System repositories (Workflow Management & Execution)
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddScoped<IStepRepository, StepRepository>();
builder.Services.AddScoped<IStepTypeRepository, StepTypeRepository>();
builder.Services.AddScoped<IStatusRepository, StatusRepository>();
builder.Services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
builder.Services.AddScoped<IWorkflowNotificationRepository, WorkflowNotificationRepository>();
builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();

// Workflow System services (Step Handlers & Execution - Phase 1)
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IWorkflowExecutionService,
    FIS.Core.Application.Services.Workflow.WorkflowExecutionService
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandlerFactory,
    FIS.Core.Application.Services.Workflow.StepHandlerFactory
>();

// Workflow Template service (Phase 2)
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IWorkflowTemplateService,
    FIS.Core.Application.Services.Workflow.WorkflowTemplateService
>();

// Condition Evaluator service (Phase 3)
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IConditionEvaluator,
    FIS.Core.Application.Services.Workflow.ConditionEvaluator
>();

// Notification services (Phase 4)
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IEmailService,
    WorkflowEmailService
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.INotificationService,
    FIS.Core.Application.Services.Workflow.NotificationService
>();

// Analytics repositories (Phase 5)
builder.Services.AddScoped<IStepExecutionHistoryRepository, StepExecutionHistoryRepository>();
builder.Services.AddScoped<IWorkflowMetricRepository, WorkflowMetricRepository>();
builder.Services.AddScoped<
    IWorkflowExecutionSummaryRepository,
    WorkflowExecutionSummaryRepository
>();

// Analytics service (Phase 5)
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IAnalyticsService,
    FIS.Core.Application.Services.Workflow.AnalyticsService
>();

// Register step handlers
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandler,
    FIS.Core.Application.Services.Workflow.Handlers.EmailNotificationHandler
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandler,
    FIS.Core.Application.Services.Workflow.Handlers.ApprovalHandler
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandler,
    FIS.Core.Application.Services.Workflow.Handlers.DataValidationHandler
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandler,
    FIS.Core.Application.Services.Workflow.Handlers.WebhookHandler
>();
builder.Services.AddScoped<
    FIS.Core.Application.Interfaces.Workflow.IStepHandler,
    FIS.Core.Application.Services.Workflow.Handlers.DelayHandler
>();

// Add HttpClient for WebhookHandler
builder.Services.AddHttpClient();

// Phase 1 & 2 missing API repositories (Merchant, ThirdParty)
builder.Services.AddScoped<IMerchantRepository, MerchantRepository>();
builder.Services.AddScoped<IThirdPartyProjectRepository, ThirdPartyProjectRepository>();
builder.Services.AddScoped<IThirdPartyAllocationRepository, ThirdPartyAllocationRepository>();

// Authentication repositories (Dual auth - Entra ID + Legacy JWT)
builder.Services.AddScoped<IEntraIdUserMappingRepository, EntraIdUserMappingRepository>();
builder.Services.AddScoped<ILegacyCredentialRepository, LegacyCredentialRepository>();

// Authentication services
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Financial system repositories
builder.Services.AddScoped<ITariffRepository, TariffRepository>();
builder.Services.AddScoped<IVehicleTariffRepository, VehicleTariffRepository>(); // ✅ Re-enabled for tariff recalculation
builder.Services.AddScoped<ITariffManagementRepository, TariffManagementRepository>();
builder.Services.AddScoped<ILeaseTariffRepository, LeaseTariffRepository>();
builder.Services.AddScoped<ITariffParameterRepository, TariffParameterRepository>();
builder.Services.AddScoped<IMaintenanceValueRepository, MaintenanceValueRepository>();
builder.Services.AddScoped<IOverheadRepository, OverheadRepository>();
builder.Services.AddScoped<ITariffWeightCalculationRepository, TariffWeightCalculationRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IInvoiceItemRepository, InvoiceItemRepository>();
builder.Services.AddScoped<IJournalDetailRepository, JournalDetailRepository>();

// Phase 3 complete: All missing API controllers implemented (Department, Driver, User, Trip)

// Register background jobs (Phase 5)
builder.Services.AddScoped<WorkflowMetricsJob>();
builder.Services.AddScoped<ContractExpiryReminderJob>();
builder.Services.AddScoped<MonthlyBillingJob>();
builder.Services.AddScoped<FinancialYearRolloverJob>();

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

    // Hangfire Dashboard (Development only)
    app.UseHangfireDashboard(
        "/hangfire",
        new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
        }
    );
}

// MUST come first: respect X-Forwarded-Proto from nginx so Request.IsHttps is correct behind the reverse proxy
app.UseForwardedHeaders();

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

// Configure recurring Hangfire jobs (Phase 5 - Analytics)
var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

// Daily workflow metrics generation (runs at 2 AM daily)
recurringJobManager.AddOrUpdate<WorkflowMetricsJob>(
    "generate-daily-workflow-metrics",
    job => job.GenerateDailyMetricsAsync(),
    "0 2 * * *"
); // Cron: Daily at 2:00 AM

// Contract expiry reminders (runs at 7 AM daily)
// Sends emails at 90, 60, 30, 14, and 7 days before target_return_date
// Recipients: site contact (client) + original capturer
recurringJobManager.AddOrUpdate<ContractExpiryReminderJob>(
    "contract-expiry-reminders",
    job => job.RunAsync(),
    "0 7 * * *"
); // Cron: Daily at 7:00 AM

// Monthly billing (runs on the 1st of each month at 06:00)
// Bills all still_current = 'Y' contracts from Charged_Until → today.
// end_date does NOT stop billing — only closing/reassigning a contract does.
recurringJobManager.AddOrUpdate<MonthlyBillingJob>(
    "monthly-contract-billing",
    job => job.RunAsync(),
    "0 6 1 * *"
); // Cron: 1st of each month at 06:00

// Financial year rollover (runs at 00:05 on 1 April every year)
// Creates the next financial_year record (FY = year it ends in).
// e.g. runs 1 April 2026 → creates FY2027 (2026-04-01 to 2027-03-31).
// Idempotent: safe to re-run, skips if record already exists.
recurringJobManager.AddOrUpdate<FinancialYearRolloverJob>(
    "financial-year-rollover",
    job => job.RunAsync(),
    "5 0 1 4 *"
); // Cron: 00:05 on 1 April each year

app.Run();
