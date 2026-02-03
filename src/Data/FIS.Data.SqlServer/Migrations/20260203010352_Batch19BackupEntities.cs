using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch19BackupEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fleet_notes_bkp",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fleet_notes_code = table.Column<int>(type: "int", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    update_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleet_notes_bkp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fleet_notes_bkp_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fleet_notes_bkp_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "fleet_notes_restore",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fleet_notes_code = table.Column<int>(type: "int", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    update_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleet_notes_restore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fleet_notes_restore_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fleet_notes_restore_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "InvalidSegmentNumbersUsed",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    segment_number = table.Column<int>(type: "int", nullable: false),
                    segment_group_code = table.Column<int>(type: "int", nullable: false),
                    segment_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    department_code = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvalidSegmentNumbersUsed", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvalidSegmentNumbersUsed_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_InvalidSegmentNumbersUsed_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Last_monthly_Tar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Last_monthly_Tar = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Last_monthly_Tar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Last_monthly_Tar_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Last_monthly_Tar_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "LeaseTariff",
                schema: "dbo",
                columns: table => new
                {
                    lease_tariff_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fixed_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseTariff", x => x.lease_tariff_code);
                    table.ForeignKey(
                        name: "FK_LeaseTariff_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseTariff_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseTariff_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaseTariff_File",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    No = table.Column<short>(type: "smallint", nullable: true),
                    VMF_Code = table.Column<int>(type: "int", nullable: true),
                    GGNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GPNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Start_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    End_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Fixed_Tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Excess_Kilo_Tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseTariff_File", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseTariff_File_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseTariff_File_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "LeaseTariff_history",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lease_tariff_code = table.Column<int>(type: "int", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fixed_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseTariff_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseTariff_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseTariff_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseTariff_history_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripsWithoutRoutes_Backup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_authority_code = table.Column<int>(type: "int", nullable: false),
                    contract_code = table.Column<int>(type: "int", nullable: false),
                    approver_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    approver_rank = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    approver_tel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    end_odo_meter = table.Column<int>(type: "int", nullable: true),
                    expiry_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    trip_reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    trip_request_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    issue_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    trip_type_code = table.Column<short>(type: "smallint", nullable: false),
                    trip_incident_type_code = table.Column<short>(type: "smallint", nullable: false),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    locked_for_transfer = table.Column<bool>(type: "bit", nullable: false),
                    Trip_Is_Monthly = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripsWithoutRoutes_Backup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripsWithoutRoutes_Backup_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TripsWithoutRoutes_Backup_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "tyda",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    xf_nom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xd_nom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xs_odo = table.Column<int>(type: "int", nullable: true),
                    xe_odo = table.Column<int>(type: "int", nullable: true),
                    xs_dat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    xe_dat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    xreknum = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xtrdat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    xclas = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tyda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tyda_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_tyda_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "tyda1",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    xf_nom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xr_nom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xd_nom = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    xd_naam = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    xs_odo = table.Column<int>(type: "int", nullable: true),
                    xe_odo = table.Column<int>(type: "int", nullable: true),
                    xs_dat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    xe_dat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    xel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tyda1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tyda1_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_tyda1_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "user_access_old1",
                schema: "dbo",
                columns: table => new
                {
                    user_access_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Site_code = table.Column<short>(type: "smallint", nullable: true),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    user_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    last_log_on = table.Column<DateTime>(type: "datetime2", nullable: true),
                    vmf = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    data_import = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    virtual_odo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    month_end = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    user_access = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccessLevel = table.Column<long>(type: "bigint", nullable: false),
                    E_Mail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    telephone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Connection_type = table.Column<int>(type: "int", nullable: true),
                    OS_Browser = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Position_Code = table.Column<byte>(type: "tinyint", nullable: true),
                    user_active = table.Column<bool>(type: "bit", nullable: false),
                    Access_str = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Retry = table.Column<short>(type: "smallint", nullable: true),
                    PWD_Expires = table.Column<DateTime>(type: "datetime2", nullable: true),
                    firtsname = table.Column<int>(type: "int", nullable: true),
                    Persal_Number = table.Column<int>(type: "int", nullable: true),
                    Contract_Number = table.Column<int>(type: "int", nullable: true),
                    sa_id_number = table.Column<int>(type: "int", nullable: true),
                    passport_number = table.Column<int>(type: "int", nullable: true),
                    Fax_Number = table.Column<int>(type: "int", nullable: true),
                    Cellphone_Number = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_access_old1", x => x.user_access_code);
                    table.ForeignKey(
                        name: "FK_user_access_old1_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_user_access_old1_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_bkp_created_by_user_code",
                schema: "dbo",
                table: "fleet_notes_bkp",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_bkp_modified_by_user_code",
                schema: "dbo",
                table: "fleet_notes_bkp",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_restore_created_by_user_code",
                schema: "dbo",
                table: "fleet_notes_restore",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_restore_modified_by_user_code",
                schema: "dbo",
                table: "fleet_notes_restore",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_InvalidSegmentNumbersUsed_created_by_user_code",
                schema: "dbo",
                table: "InvalidSegmentNumbersUsed",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_InvalidSegmentNumbersUsed_modified_by_user_code",
                schema: "dbo",
                table: "InvalidSegmentNumbersUsed",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Last_monthly_Tar_created_by_user_code",
                schema: "dbo",
                table: "Last_monthly_Tar",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Last_monthly_Tar_modified_by_user_code",
                schema: "dbo",
                table: "Last_monthly_Tar",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_created_by_user_code",
                schema: "dbo",
                table: "LeaseTariff",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_modified_by_user_code",
                schema: "dbo",
                table: "LeaseTariff",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_vmf_code",
                schema: "dbo",
                table: "LeaseTariff",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_File_created_by_user_code",
                schema: "dbo",
                table: "LeaseTariff_File",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_File_modified_by_user_code",
                schema: "dbo",
                table: "LeaseTariff_File",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_history_created_by_user_code",
                schema: "dbo",
                table: "LeaseTariff_history",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_history_modified_by_user_code",
                schema: "dbo",
                table: "LeaseTariff_history",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTariff_history_vmf_code",
                schema: "dbo",
                table: "LeaseTariff_history",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_TripsWithoutRoutes_Backup_created_by_user_code",
                schema: "dbo",
                table: "TripsWithoutRoutes_Backup",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TripsWithoutRoutes_Backup_modified_by_user_code",
                schema: "dbo",
                table: "TripsWithoutRoutes_Backup",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tyda_created_by_user_code",
                schema: "dbo",
                table: "tyda",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tyda_modified_by_user_code",
                schema: "dbo",
                table: "tyda",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tyda1_created_by_user_code",
                schema: "dbo",
                table: "tyda1",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tyda1_modified_by_user_code",
                schema: "dbo",
                table: "tyda1",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_access_old1_created_by_user_code",
                schema: "dbo",
                table: "user_access_old1",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_access_old1_modified_by_user_code",
                schema: "dbo",
                table: "user_access_old1",
                column: "modified_by_user_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fleet_notes_bkp",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "fleet_notes_restore",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InvalidSegmentNumbersUsed",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Last_monthly_Tar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LeaseTariff",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LeaseTariff_File",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LeaseTariff_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TripsWithoutRoutes_Backup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "tyda",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "tyda1",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "user_access_old1",
                schema: "dbo");
        }
    }
}
