using FIS.Core.Domain.Entities;
using FIS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FIS.Data.SqlServer;

/// <summary>
/// FIXED DbContext with PROPER constraints and business rule enforcement
///
/// CRITICAL FIXES:
/// - Unique constraints preventing duplicate active contracts per vehicle
/// - Proper foreign key relationships with cascading rules
/// - Business rule validation at database level
/// - Fixed data integrity constraints that were disabled in legacy system
/// </summary>
public class FisDbContext : DbContext
{
    public FisDbContext(DbContextOptions<FisDbContext> options)
        : base(options) { }

    // FIXED Entity Sets with exact legacy schema mapping
    // Core legacy entities mapped to actual database schema from Database.cs
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
    public DbSet<Contract> Contracts { get; set; } = null!;
    public DbSet<Site> Sites { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!; // Maps to TS_Users table

    // Legacy entities based on actual table schemas from Database.cs
    public DbSet<FuelCard> FuelCards { get; set; } = null!;
    public DbSet<TripDriver> TripDrivers { get; set; } = null!;
    public DbSet<PrivateHire> PrivateHires { get; set; } = null!;
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<Trip> Trips { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;

    // Phase 1 entities - Core vehicle management
    public DbSet<Make> Makes { get; set; } = null!;
    public DbSet<Model> Models { get; set; } = null!;
    public DbSet<FIS.Data.Entities.Type> Types { get; set; } = null!;
    public DbSet<FuelType> FuelTypes { get; set; } = null!;
    public DbSet<MaintenanceTrigger> MaintenanceTriggers { get; set; } = null!;
    public DbSet<License> Licenses { get; set; } = null!;
    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; } = null!;
    public DbSet<ContractStatus> ContractStatuses { get; set; } = null!;

    // Additional Phase 4 entities
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; } = null!;

    // Financial entities
    public DbSet<FIS.Core.Domain.Entities.Financial.Invoice> Invoices { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.Financial.InvoiceItem> InvoiceItems { get; set; } = null!;

    // Operations entities (Fleet management operations)
    public DbSet<Auction> Auctions { get; set; } = null!;
    public DbSet<CallCentre> CallCentres { get; set; } = null!;
    public DbSet<Clearance> Clearances { get; set; } = null!;
    public DbSet<Fine> Fines { get; set; } = null!;
    public DbSet<Logbook> Logbooks { get; set; } = null!;
    public DbSet<Logsheet> Logsheets { get; set; } = null!;
    public DbSet<Loss> Losses { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.Monitor> Monitors { get; set; } = null!;
    public DbSet<Taxi> Taxis { get; set; } = null!;
    public DbSet<Towing> Towings { get; set; } = null!;
    // TripAuthority removed - conflicts with Trip entity (both map to trip_authorities table)
    public DbSet<VehicleOrder> VehicleOrders { get; set; } = null!;
    public DbSet<VehiclePhoto> VehiclePhotos { get; set; } = null!;
    public DbSet<Workshop> Workshops { get; set; } = null!;

    // Batch 3 entities (Accident management and vehicle tracking)
    public DbSet<Accident> Accidents { get; set; } = null!;
    public DbSet<Tracking> Trackings { get; set; } = null!;
    public DbSet<VehicleAssessment> VehicleAssessments { get; set; } = null!;
    public DbSet<VehicleDamage> VehicleDamages { get; set; } = null!;
    public DbSet<TrafficDept> TrafficDepts { get; set; } = null!;

    // Batch 4 entities (Asset verification, lease, bookings, suppliers)
    public DbSet<AssetVerification> AssetVerifications { get; set; } = null!;
    public DbSet<LeaseContractTerms> LeaseContractTerms { get; set; } = null!;
    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // VEHICLE CONFIGURATION - Legacy database compatibility
        modelBuilder.Entity<Vehicle>(entity =>
        {
            // Entity is already configured with [Table("vehicle_master")] attribute
            // Only need to configure indexes and constraints for legacy schema

            // CRITICAL FIX: Unique constraint on VMF Code (was missing in legacy)
            entity
                .HasIndex(e => e.vmf_code)
                .IsUnique()
                .HasDatabaseName("IX_Vehicle_VmfCode_Unique");

            // CRITICAL FIX: Unique constraint on Registration Number
            entity
                .HasIndex(e => e.registration_number)
                .IsUnique()
                .HasDatabaseName("IX_Vehicle_Registration_Unique");

            // Business rule validation with legacy column names
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Vehicle_PurchasePrice", "purchase_amount >= 0");
                t.HasCheckConstraint("CK_Vehicle_CurrentOdometer", "current_odo >= 0");
            });
        });

        // CONTRACT CONFIGURATION - Legacy schema compatibility with business rules
        modelBuilder.Entity<Contract>(entity =>
        {
            // Entity is already configured with [Table("contract")] attribute

            // CRITICAL FIX: Unique constraint preventing multiple active contracts per vehicle
            // Updated for legacy char(1) format: still_current = 'Y' instead of StillCurrent = 1
            entity
                .HasIndex(e => new { e.vmf_code, e.still_current })
                .HasFilter("still_current = 'Y'")
                .IsUnique()
                .HasDatabaseName("IX_Contract_ActiveVehicle_Unique");

            // Business rule constraints using legacy column names
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Contract_StartOdometer", "start_odometer >= 0");
                t.HasCheckConstraint(
                    "CK_Contract_EndOdometer",
                    "end_odometer IS NULL OR end_odometer > start_odometer"
                );
                t.HasCheckConstraint(
                    "CK_Contract_Dates",
                    "end_date IS NULL OR end_date > start_date"
                );
                t.HasCheckConstraint("CK_Contract_StillCurrent", "still_current IN ('Y', 'N')");
            });
        });

        // SITE CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Site>(entity =>
        {
            // Entity is already configured with [Table("site")] attribute
            entity.HasKey(e => e.Site_code);

            // Note: Depatrment_code typo is intentional legacy compatibility
            // Use description field as site identifier
            entity
                .HasIndex(e => e.description)
                .IsUnique()
                .HasDatabaseName("IX_Site_Description_Unique");
        });

        // FUEL CARD CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<FuelCard>(entity =>
        {
            // Entity is already configured with [Table("Fuel_card")] attribute
            entity
                .HasIndex(e => e.Fuel_card_code)
                .IsUnique()
                .HasDatabaseName("IX_FuelCard_Code_Unique");
        });

        // TRIP DRIVER CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<TripDriver>(entity =>
        {
            // Entity is already configured with [Table("trip_driver")] attribute
            entity.HasKey(e => e.trip_driver_code);
        });

        // PRIVATE HIRE CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<PrivateHire>(entity =>
        {
            // Entity is already configured with [Table("Private_hire")] attribute
            entity.HasKey(e => e.PHV_code);
        });

        // DRIVER CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Driver>(entity =>
        {
            // Entity is already configured with [Table("site_drivers")] attribute
            entity.HasKey(e => e.site_driver_code);
        });

        // TRIP CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Trip>(entity =>
        {
            // Entity is already configured with [Table("trip_authorities")] attribute
            entity.HasKey(e => e.trip_authority_code);
        });

        // DEPARTMENT CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Department>(entity =>
        {
            // Entity is already configured with [Table("department")] attribute
            entity.HasKey(e => e.department_code);
        });

        // USER CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<User>(entity =>
        {
            // Entity is already configured with [Table("TS_Users")] attribute
            entity.HasKey(e => e.user_access_code);
        });

        // MAKE CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Make>(entity =>
        {
            // Entity is already configured with [Table("make")] attribute
            entity.HasKey(e => e.make_code);

            // Configure one-to-many relationship with Model
            entity
                .HasMany(m => m.Models)
                .WithOne(model => model.Make)
                .HasForeignKey(model => model.make_code)
                .HasConstraintName("FK_Model_Make");
        });

        // MODEL CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<Model>(entity =>
        {
            // Entity is already configured with [Table("model")] attribute
            entity.HasKey(e => e.model_code);

            // Configure foreign key relationship with Make
            entity
                .HasOne(model => model.Make)
                .WithMany(m => m.Models)
                .HasForeignKey(model => model.make_code)
                .HasConstraintName("FK_Model_Make")
                .OnDelete(DeleteBehavior.Restrict); // Prevent accidental make deletion
        });

        // TYPE CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<FIS.Data.Entities.Type>(entity =>
        {
            // Entity is already configured with [Table("type")] attribute
            entity.HasKey(e => e.type_code);

            // Note: Vehicle entity has type_code foreign key but no navigation property
            // We'll configure this separately if needed for queries
        });

        // FUEL TYPE CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<FuelType>(entity =>
        {
            // Entity is already configured with [Table("fuel_type")] attribute
            entity.HasKey(e => e.fuel_type_code);

            // Note: Model entity has fuel_type_code foreign key field but no navigation property
            // This is intentional for legacy schema compatibility
        });

        // MAINTENANCE TRIGGER CONFIGURATION - Legacy schema compatibility
        modelBuilder.Entity<MaintenanceTrigger>(entity =>
        {
            // Entity is already configured with [Table("maintenance_trigger")] attribute
            entity.HasKey(e => e.maint_trigger_code);

            // Note: Model entity has maint_trigger_code foreign key field but no navigation property
            // This is intentional for legacy schema compatibility
        });

        // Configure schema
        modelBuilder.HasDefaultSchema("dbo");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Additional configuration if needed
        if (!optionsBuilder.IsConfigured)
        {
            // This will be overridden by dependency injection configuration
            optionsBuilder.UseSqlServer(
                "Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;"
            );
        }
    }
}
