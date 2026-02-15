using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Domain.Entities.Integration;
using FIS.Core.Domain.Entities.Locations;
using FIS.Core.Domain.Entities.Logistics;
using FIS.Core.Domain.Entities.Maintenance;
using FIS.Core.Domain.Entities.Operations;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.System;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Core.Domain.Entities.WorkshopEntities;
using Microsoft.EntityFrameworkCore;

namespace FIS.Data.SqlServer;

public class FisDbContext : DbContext
{
    public FisDbContext(DbContextOptions<FisDbContext> options)
        : base(options) { }

    // Core Legacy Entities
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
    public DbSet<Contract> Contracts { get; set; } = null!;

    // Batch 8 Contract entities
    public DbSet<ContractType> ContractTypes { get; set; } = null!;
    public DbSet<ContractTypeGroupMapping> ContractTypeGroupMappings { get; set; } = null!;
    public DbSet<Contractor> Contractors { get; set; } = null!;
    public DbSet<ContractorTaxiClass> ContractorTaxiClasses { get; set; } = null!;
    public DbSet<ContractRebillSplit> ContractRebillSplits { get; set; } = null!;

    public DbSet<Site> Sites { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!; // Maps to TS_Users table
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<DriverLicence> DriverLicences { get; set; } = null!;
    public DbSet<DriverLicenceType> DriverLicenceTypes { get; set; } = null!;
    public DbSet<Trip> Trips { get; set; } = null!;
    public DbSet<TripDriver> TripDrivers { get; set; } = null!;
    public DbSet<PrivateHire> PrivateHires { get; set; } = null!;
    public DbSet<FuelCard> FuelCards { get; set; } = null!;

    // Reference Data Entities
    public DbSet<Make> Makes { get; set; } = null!;
    public DbSet<Model> Models { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.Vehicles.Class> Classes { get; set; } = null!;
    public DbSet<FuelType> FuelTypes { get; set; } = null!;
    public DbSet<MaintenanceTrigger> MaintenanceTriggers { get; set; } = null!;
    public DbSet<License> Licenses { get; set; } = null!;
    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; } = null!;
    public DbSet<ContractStatus> ContractStatuses { get; set; } = null!;
    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;

    // Batch 9 Locations entities
    public DbSet<Ambulance> Ambulances { get; set; } = null!;
    public DbSet<TowTruck> TowTrucks { get; set; } = null!;
    public DbSet<Univ> Univs { get; set; } = null!;

    // Batch 9 Logistics entities
    public DbSet<TripIncidentType> TripIncidentTypes { get; set; } = null!;
    public DbSet<TripPassenger> TripPassengers { get; set; } = null!;
    public DbSet<TripType> TripTypes { get; set; } = null!;

    // Financial Entities
    public DbSet<FIS.Core.Domain.Entities.Financial.Invoice> Invoices { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.Financial.InvoiceItem> InvoiceItems { get; set; } = null!;
    public DbSet<AbsaTransaction> AbsaTransactions { get; set; } = null!;
    public DbSet<AbsaTransactionCode> AbsaTransactionCodes { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<BudgetAmount> BudgetAmounts { get; set; } = null!;
    public DbSet<BurnRate> BurnRates { get; set; } = null!;
    public DbSet<CostCategory> CostCategories { get; set; } = null!;
    public DbSet<OverheadType> OverheadTypes { get; set; } = null!;
    public DbSet<RT57> RT57s { get; set; } = null!;
    public DbSet<VipBilling> VipBillings { get; set; } = null!;
    public DbSet<WesbankTransaction> WesbankTransactions { get; set; } = null!;
    public DbSet<DailyTransaction> DailyTransactions { get; set; } = null!;
    public DbSet<Batch> Batches { get; set; } = null!;
    public DbSet<BatchExport> BatchExports { get; set; } = null!;
    public DbSet<DailyImportExcept> DailyImportExcepts { get; set; } = null!;
    public DbSet<JournalHeader> JournalHeaders { get; set; } = null!;
    public DbSet<JournalDetailType> JournalDetailTypes { get; set; } = null!;
    public DbSet<JournalDetailTypeGroup> JournalDetailTypeGroups { get; set; } = null!;
    public DbSet<PostingMonth> PostingMonths { get; set; } = null!;
    public DbSet<FinancialSystem> FinancialSystems { get; set; } = null!;
    public DbSet<FinancialYear> FinancialYears { get; set; } = null!;
    public DbSet<IncomeSplitTempTable> IncomeSplitTempTables { get; set; } = null!;
    public DbSet<JournalWithInvalidBasCode> JournalWithInvalidBasCodes { get; set; } = null!;
    public DbSet<MonthlyBurnRate> MonthlyBurnRates { get; set; } = null!;
    public DbSet<MonthlyPoolVehicle> MonthlyPoolVehicles { get; set; } = null!;
    public DbSet<CostRevenueMap> CostRevenueMaps { get; set; } = null!;
    public DbSet<FuelRecoveryConfiguration> FuelRecoveryConfigurations { get; set; } = null!;
    public DbSet<MaintenanceValue> MaintenanceValues { get; set; } = null!;
    public DbSet<VehicleType> VehicleTypes { get; set; } = null!;
    public DbSet<LeaseContractTermsComment> LeaseContractTermsComments { get; set; } = null!;
    public DbSet<NotifyList> NotifyLists { get; set; } = null!;
    public DbSet<PrivateHireFuelCard> PrivateHireFuelCards { get; set; } = null!;
    public DbSet<ProfileHistory> ProfileHistories { get; set; } = null!;
    public DbSet<TSComment> TSComments { get; set; } = null!;
    public DbSet<TSErrorCode> TSErrorCodes { get; set; } = null!;
    public DbSet<Surcharge> Surcharges { get; set; } = null!;
    public DbSet<MaintenanceValueOriginal> MaintenanceValueOriginals { get; set; } = null!;
    public DbSet<MaintenanceValueTransfer> MaintenanceValueTransfers { get; set; } = null!;
    public DbSet<SystemParameter> SystemParameters { get; set; } = null!;
    public DbSet<SsisConfiguration> SsisConfigurations { get; set; } = null!;
    public DbSet<Holiday> Holidays { get; set; } = null!;
    public DbSet<Il> Ils { get; set; } = null!;
    public DbSet<MonthlyOdo> MonthlyOdos { get; set; } = null!;
    public DbSet<RequestChange> RequestChanges { get; set; } = null!;
    public DbSet<ResPersonHistory> ResPersonHistories { get; set; } = null!;
    public DbSet<Registration> Registrations { get; set; } = null!;
    public DbSet<ReportVehicle> ReportVehicles { get; set; } = null!;
    public DbSet<RequestNumber> RequestNumbers { get; set; } = null!;
    public DbSet<RouteDetail> RouteDetails { get; set; } = null!;
    public DbSet<GgBlock> GgBlocks { get; set; } = null!;
    public DbSet<MerchantReference> MerchantReferences { get; set; } = null!;
    public DbSet<MfCode> MfCodes { get; set; } = null!;
    public DbSet<MfCodeMap> MfCodeMaps { get; set; } = null!;
    public DbSet<PostingYear> PostingYears { get; set; } = null!;
    public DbSet<HistoryStatus> HistoryStatuses { get; set; } = null!;
    public DbSet<ScanDoc> ScanDocs { get; set; } = null!;
    public DbSet<TaxiScanDoc> TaxiScanDocs { get; set; } = null!;
    public DbSet<TaxiLog> TaxiLogs { get; set; } = null!;
    public DbSet<TaxiWhiteLog> TaxiWhiteLogs { get; set; } = null!;
    public DbSet<ReportTemp> ReportTemps { get; set; } = null!;
    public DbSet<FleetNoteBackup> FleetNoteBackups { get; set; } = null!;
    public DbSet<FleetNoteRestore> FleetNoteRestores { get; set; } = null!;
    public DbSet<TripWithoutRouteBackup> TripWithoutRouteBackups { get; set; } = null!;
    public DbSet<UserAccessOld> UserAccessOlds { get; set; } = null!;
    public DbSet<Tyda> Tydas { get; set; } = null!;
    public DbSet<Tyda1> Tyda1s { get; set; } = null!;
    public DbSet<InvalidSegmentNumber> InvalidSegmentNumbers { get; set; } = null!;
    public DbSet<Tariff> Tariffs { get; set; } = null!;
    public DbSet<LastMonthlyTariff> LastMonthlyTariffs { get; set; } = null!;
    public DbSet<LeaseTariff> LeaseTariffs { get; set; } = null!;
    public DbSet<LeaseTariffHistory> LeaseTariffHistories { get; set; } = null!;
    public DbSet<LeaseTariffFile> LeaseTariffFiles { get; set; } = null!;
    public DbSet<Segment> Segments { get; set; } = null!;
    public DbSet<SegmentGroup> SegmentGroups { get; set; } = null!;
    public DbSet<SegmentScoa> SegmentScoas { get; set; } = null!;
    public DbSet<SegmentStructureMap> SegmentStructureMaps { get; set; } = null!;
    public DbSet<SegmentJournalDetailMap> SegmentJournalDetailMaps { get; set; } = null!;
    public DbSet<JournalDetailTypeSegmentGroupMap> JournalDetailTypeSegmentGroupMaps { get; set; } = null!;
    public DbSet<JournalDetailAllocationException> JournalDetailAllocationExceptions { get; set; } = null!;
    public DbSet<Parameter> Parameters { get; set; } = null!;
    public DbSet<ParameterValue> ParameterValues { get; set; } = null!;
    public DbSet<UserCompany> UserCompanies { get; set; } = null!;
    public DbSet<ContractStatusHistory> ContractStatusHistories { get; set; } = null!;
    public DbSet<ContractAuditLog> ContractAuditLogs { get; set; } = null!;
    public DbSet<ContractTypeGrouping> ContractTypeGroupings { get; set; } = null!;
    public DbSet<ContractTypeMap> ContractTypeMaps { get; set; } = null!;
    public DbSet<ContractTypeMapping> ContractTypeMappings { get; set; } = null!;
    public DbSet<DtProperty> DtProperties { get; set; } = null!;
    public DbSet<FisSurvey> FisSurveys { get; set; } = null!;
    public DbSet<CallCentreCounter> CallCentreCounters { get; set; } = null!;
    // public DbSet<MaintTriggerType> MaintTriggerTypes { get; set; } = null!;
    public DbSet<MaintenanceValueHistory> MaintenanceValueHistories { get; set; } = null!;
    public DbSet<Overhead> Overheads { get; set; } = null!;
    public DbSet<TariffParameter> TariffParameters { get; set; } = null!;
    public DbSet<TariffWeightCalculation> TariffWeightCalculations { get; set; } = null!;
    public DbSet<VehicleTariff> VehicleTariffs { get; set; } = null!;

    // Operations Entities
    public DbSet<Accident> Accidents { get; set; } = null!;
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
    public DbSet<VehicleOrder> VehicleOrders { get; set; } = null!;
    public DbSet<VehiclePhoto> VehiclePhotos { get; set; } = null!;
    public DbSet<Extra> Extras { get; set; } = null!;
    public DbSet<JobCard> JobCards { get; set; } = null!;
    public DbSet<Workshop> Workshops { get; set; } = null!;
    public DbSet<Tracking> Trackings { get; set; } = null!;
    public DbSet<VehicleAssessment> VehicleAssessments { get; set; } = null!;
    public DbSet<VehicleDamage> VehicleDamages { get; set; } = null!;
    public DbSet<TrafficDept> TrafficDepts { get; set; } = null!;
    public DbSet<AssetVerification> AssetVerifications { get; set; } = null!;
    public DbSet<LeaseContractTerms> LeaseContractTerms { get; set; } = null!;
    public DbSet<Booking> Bookings { get; set; } = null!;
    public DbSet<Collection> Collections { get; set; } = null!;

    // Workshop & Parts
    public DbSet<Part> Parts { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.WorkshopEntities.Task> Tasks { get; set; } = null!;
    public DbSet<Merchant> Merchants { get; set; } = null!;
    public DbSet<MaintenanceProfileModel> MaintenanceProfileModels { get; set; } = null!;

    // Batch 6 Integration entities
    public DbSet<PastelCustomer> PastelCustomers { get; set; } = null!;
    public DbSet<PastelGL> PastelGLs { get; set; } = null!;
    public DbSet<PastelStatic> PastelStatics { get; set; } = null!;
    public DbSet<ThirdPartyVehicleModelDescription> ThirdPartyVehicleModelDescriptions { get; set; } = null!;
    public DbSet<GGBlock> GGBlocks { get; set; } = null!;
    public DbSet<BlockGGNumber> BlockGGNumbers { get; set; } = null!;

    // Maintenance Entities
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; } = null!;

    // Authentication Entities
    public DbSet<EntraIdUserMapping> EntraIdUserMappings { get; set; } = null!;
    public DbSet<LegacyUserCredential> LegacyUserCredentials { get; set; } = null!;
    public DbSet<AccessLevel> AccessLevels { get; set; } = null!;
    public DbSet<AccessLevels2> AccessLevels2s { get; set; } = null!;
    public DbSet<Approver> Approvers { get; set; } = null!;
    public DbSet<UserMessage> UserMessages { get; set; } = null!;

    // System Entities
    public DbSet<TSLog> TSLogs { get; set; } = null!;
    public DbSet<Audit> Audits { get; set; } = null!;
    public DbSet<ErrorLog> ErrorLogs { get; set; } = null!;
    public DbSet<BookingAddress> BookingAddresses { get; set; } = null!;
    public DbSet<EventMap> EventMaps { get; set; } = null!;
    
    // Workflow System entities (Phase 1-5)
    public DbSet<FIS.Core.Domain.Entities.System.Status> Statuses { get; set; } = null!;
    public DbSet<Step> Steps { get; set; } = null!;
    public DbSet<StepType> StepTypes { get; set; } = null!;
    public DbSet<FIS.Core.Domain.Entities.System.Workflow> Workflows { get; set; } = null!;
    public DbSet<WorkflowTemplate> WorkflowTemplates { get; set; } = null!;
    public DbSet<WorkflowNotification> WorkflowNotifications { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;
    public DbSet<StepExecutionHistory> StepExecutionHistories { get; set; } = null!;
    public DbSet<WorkflowMetric> WorkflowMetrics { get; set; } = null!;
    public DbSet<WorkflowExecutionSummary> WorkflowExecutionSummaries { get; set; } = null!;

    // Notice Management System entities
    public DbSet<Notice> Notices { get; set; } = null!;
    public DbSet<NoticeSchedule> NoticeSchedules { get; set; } = null!;

    // Third Party / Supplier Management entities
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<ClassRequirement> ClassRequirements { get; set; } = null!;

    // Batch 1 Reference Data (Lookups)
    public DbSet<Province> Provinces { get; set; } = null!;
    public DbSet<Rank> Ranks { get; set; } = null!;
    public DbSet<Position> Positions { get; set; } = null!;
    public DbSet<EduCode> EduCodes { get; set; } = null!;
    public DbSet<IncidentArea> IncidentAreas { get; set; } = null!;
    public DbSet<VehicleSource> VehicleSources { get; set; } = null!;
    public DbSet<VehicleStatus> VehicleStatuses { get; set; } = null!;
    public DbSet<FuelTariff> FuelTariffs { get; set; } = null!;
    public DbSet<ExtraCode> ExtraCodes { get; set; } = null!;
    public DbSet<LicenseFee> LicenseFees { get; set; } = null!;
    public DbSet<LossType> LossTypes { get; set; } = null!;
    public DbSet<AccType> AccTypes { get; set; } = null!;
    public DbSet<AdHocHoliday> AdHocHolidays { get; set; } = null!;
    public DbSet<BasSegment> BasSegments { get; set; } = null!;
    public DbSet<BillOfMaterial> BillOfMaterials { get; set; } = null!;

    // Batch 7 Reference Data entities
    public DbSet<AnchorPoint> AnchorPoints { get; set; } = null!;
    public DbSet<AnchorType> AnchorTypes { get; set; } = null!;
    public DbSet<Alphabet> Alphabets { get; set; } = null!;
    public DbSet<BitmaskDef> BitmaskDefs { get; set; } = null!;
    public DbSet<ConnectionType> ConnectionTypes { get; set; } = null!;
    public DbSet<OvertimeMultiplier> OvertimeMultipliers { get; set; } = null!;
    public DbSet<PurchaseCategory> PurchaseCategories { get; set; } = null!;
    public DbSet<SegmentType> SegmentTypes { get; set; } = null!;
    public DbSet<Tally> Tallies { get; set; } = null!;
    public DbSet<ProvinceSegmentMap> ProvinceSegmentMaps { get; set; } = null!;

    // Batch 7 System entities
    public DbSet<VersionInfo> VersionInfos { get; set; } = null!;
    public DbSet<DbVersion> DbVersions { get; set; } = null!;
    public DbSet<DbDdlLog> DbDdlLogs { get; set; } = null!;

    // Batch 4 Vehicle History Entities
    public DbSet<DemoVehicle> DemoVehicles { get; set; } = null!;
    public DbSet<NewVehicleReceived> NewVehiclesReceived { get; set; } = null!;
    public DbSet<PreVehicleMaster> PreVehicleMasters { get; set; } = null!;
    public DbSet<PreVehicleMasterNote> PreVehicleMasterNotes { get; set; } = null!;
    public DbSet<VehicleHistory> VehicleHistories { get; set; } = null!;
    public DbSet<VehicleStatusHistory> VehicleStatusHistories { get; set; } = null!;
    public DbSet<VehicleTypeHistory> VehicleTypeHistories { get; set; } = null!;
    public DbSet<VehicleKilo> VehicleKilos { get; set; } = null!;
    public DbSet<ModelKilosPerFuelLitre> ModelKilosPerFuelLitres { get; set; } = null!;
    public DbSet<WesbankKilosPerFuelLitre> WesbankKilosPerFuelLitres { get; set; } = null!;
    public DbSet<EnjinNumber> EnjinNumbers { get; set; } = null!;
    public DbSet<FleetNote> FleetNotes { get; set; } = null!;
    public DbSet<TempVehicleExtra> TempVehicleExtras { get; set; } = null!;
    public DbSet<VehicleRemark> VehicleRemarks { get; set; } = null!;
    public DbSet<VehicleLicenceHistory> VehicleLicenceHistories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(e => e.vmf_code).IsUnique().HasDatabaseName("IX_Vehicle_VmfCode_Unique");
            entity.HasIndex(e => e.registration_number).IsUnique().HasDatabaseName("IX_Vehicle_Registration_Unique");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Vehicle_PurchasePrice", "purchase_amount >= 0");
                t.HasCheckConstraint("CK_Vehicle_CurrentOdometer", "current_odo >= 0");
            });
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasIndex(e => new { e.vmf_code, e.still_current })
                .HasFilter("still_current = 'Y'")
                .IsUnique()
                .HasDatabaseName("IX_Contract_ActiveVehicle_Unique");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Contract_StartOdometer", "start_odometer >= 0");
                t.HasCheckConstraint("CK_Contract_EndOdometer", "end_odometer IS NULL OR end_odometer > start_odometer");
                t.HasCheckConstraint("CK_Contract_Dates", "end_date IS NULL OR end_date > start_date");
                t.HasCheckConstraint("CK_Contract_StillCurrent", "still_current IN ('Y', 'N')");
            });
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasKey(e => e.Site_code);
            entity.HasIndex(e => e.description).IsUnique().HasDatabaseName("IX_Site_Description_Unique");
        });

        modelBuilder.Entity<FuelCard>(entity =>
        {
            entity.HasIndex(e => e.Fuel_card_code).IsUnique().HasDatabaseName("IX_FuelCard_Code_Unique");
        });

        modelBuilder.Entity<TripDriver>(entity => entity.HasKey(e => e.trip_driver_code));
        modelBuilder.Entity<PrivateHire>(entity => entity.HasKey(e => e.PHV_code));
        modelBuilder.Entity<Driver>(entity => entity.HasKey(e => e.site_driver_code));
        modelBuilder.Entity<Trip>(entity => entity.HasKey(e => e.trip_authority_code));
        modelBuilder.Entity<Department>(entity => entity.HasKey(e => e.department_code));
        modelBuilder.Entity<User>(entity => entity.HasKey(e => e.user_access_code));

        modelBuilder.Entity<Make>(entity =>
        {
            entity.HasKey(e => e.make_code);
            entity.HasMany(m => m.Models).WithOne(model => model.Make)
                .HasForeignKey(model => model.make_code).HasConstraintName("FK_Model_Make");
        });

        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasKey(e => e.model_code);
            entity.HasOne(model => model.Make).WithMany(m => m.Models)
                .HasForeignKey(model => model.make_code).HasConstraintName("FK_Model_Make")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Batch 1 Reference Data configuration
        modelBuilder.Entity<FuelType>(entity => entity.HasKey(e => e.fuel_type_code));
        modelBuilder.Entity<MaintenanceTrigger>(entity => entity.HasKey(e => e.maint_trigger_code));

        modelBuilder.Entity<MaintenanceValue>(entity =>
        {
            entity.HasKey(e => new { e.TariffParameterID, e.class_code, e.months_age });
        });

        modelBuilder.Entity<BitmaskDef>(entity =>
        {
            entity.HasKey(e => new { e.function, e.mask });
        });

        modelBuilder.Entity<ProvinceSegmentMap>(entity =>
        {
            entity.HasKey(e => new { e.Province_code, e.segment_code });
        });

        modelBuilder.Entity<ContractRebillSplit>(entity =>
        {
            entity.HasOne(e => e.Contract).WithMany().HasForeignKey(e => e.contract_code)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Site).WithMany().HasForeignKey(e => e.site_code)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EntraIdUserMapping>(entity =>
        {
            entity.HasIndex(e => e.entra_object_id).IsUnique().HasDatabaseName("IX_EntraId_User_Mapping_ObjectId_Unique");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.user_access_code)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_EntraId_User_Mapping_User");
            entity.Property(e => e.created_date).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<LegacyUserCredential>(entity =>
        {
            entity.HasIndex(e => e.user_access_code).IsUnique().HasDatabaseName("IX_Legacy_User_Credentials_UserAccessCode_Unique");
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.user_access_code)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Legacy_User_Credentials_User");
            entity.Property(e => e.created_date).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.last_password_change).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.failed_login_attempts).HasDefaultValue(0);
            entity.Property(e => e.is_active).HasDefaultValue(true);
        });

        // JobCard constraints and indexes
        modelBuilder.Entity<JobCard>(entity =>
        {
            // Unique constraint: Only one active jobcard per vehicle+extra combination
            entity.HasIndex(e => new { e.vmf_code, e.extra_code })
                .HasFilter("is_deleted = 0 AND status_code NOT IN (5, 7)") // Not complete or canceled
                .IsUnique()
                .HasDatabaseName("UX_JobCard_Vehicle_Extra_Active");

            // Index for priority queries
            entity.HasIndex(e => new { e.priority, e.assigned_to, e.status_code })
                .HasDatabaseName("IX_JobCard_Priority_Assigned_Status");

            // Index for authorizer queries
            entity.HasIndex(e => new { e.authorizer, e.status_code })
                .HasDatabaseName("IX_JobCard_Authorizer_Status");
        });

        // VehicleLicenceHistory index — fast lookup per vehicle ordered by date
        modelBuilder.Entity<VehicleLicenceHistory>(entity =>
        {
            entity.HasIndex(e => new { e.vmf_code, e.captured_at })
                .HasDatabaseName("IX_VehicleLicenceHistory_Vehicle_Date");
        });

        // VehicleRemark indexes
        modelBuilder.Entity<VehicleRemark>(entity =>
        {
            // Fast lookup: all open remarks for a vehicle
            entity.HasIndex(e => new { e.vmf_code, e.is_resolved, e.is_deleted })
                .HasDatabaseName("IX_VehicleRemark_Vehicle_Active");

            // For the fleet-wide active remarks query
            entity.HasIndex(e => new { e.is_resolved, e.is_deleted })
                .HasDatabaseName("IX_VehicleRemark_Active");
        });

        modelBuilder.HasDefaultSchema("dbo");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;");
        }
    }
}