using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Services;
using FIS.Core.Application.Services.Billing;
using FIS.Core.Application.Services.Validation;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Core.Infrastructure.Repositories;
using FIS.Core.Infrastructure.Services;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

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

// Add health check endpoints
app.MapHealthChecks("/health");

// Add controllers
app.MapControllers();

app.Run();
