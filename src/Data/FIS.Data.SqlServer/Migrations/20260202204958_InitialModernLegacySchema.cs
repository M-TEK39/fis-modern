using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialModernLegacySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "fin");

            migrationBuilder.EnsureSchema(
                name: "Workflow");

            migrationBuilder.CreateTable(
                name: "fuel_type",
                schema: "dbo",
                columns: table => new
                {
                    fuel_type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fuel_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_type", x => x.fuel_type_code);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_trigger",
                schema: "dbo",
                columns: table => new
                {
                    maint_trigger_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    trigger_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_trigger", x => x.maint_trigger_code);
                });

            migrationBuilder.CreateTable(
                name: "TS_Users",
                schema: "dbo",
                columns: table => new
                {
                    user_access_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tel_no = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Users", x => x.user_access_code);
                    table.ForeignKey(
                        name: "FK_TS_Users_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TS_Users_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "type",
                schema: "dbo",
                columns: table => new
                {
                    type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_type", x => x.type_code);
                });

            migrationBuilder.CreateTable(
                name: "absa_transaction_codes",
                schema: "dbo",
                columns: table => new
                {
                    absa_transaction_codes_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    transaction_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    cost_category_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absa_transaction_codes", x => x.absa_transaction_codes_code);
                    table.ForeignKey(
                        name: "FK_absa_transaction_codes_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_absa_transaction_codes_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "acc_type",
                schema: "dbo",
                columns: table => new
                {
                    acc_type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    acc_type_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acc_type", x => x.acc_type_code);
                    table.ForeignKey(
                        name: "FK_acc_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_acc_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "AccessLevels",
                schema: "dbo",
                columns: table => new
                {
                    AccessLevelID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessLevelName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AccessLevelValue = table.Column<long>(type: "bigint", nullable: false),
                    AccessLevelCalc = table.Column<long>(type: "bigint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLevels", x => x.AccessLevelID);
                    table.ForeignKey(
                        name: "FK_AccessLevels_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_AccessLevels_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "AccessLevels_2",
                schema: "dbo",
                columns: table => new
                {
                    AccessLevelID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessLevelName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AccessLevelValue = table.Column<long>(type: "bigint", nullable: false),
                    AccessLevelCalc = table.Column<long>(type: "bigint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLevels_2", x => x.AccessLevelID);
                    table.ForeignKey(
                        name: "FK_AccessLevels_2_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_AccessLevels_2_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "AdHocHolidays",
                schema: "dbo",
                columns: table => new
                {
                    AdHocHolidayID = table.Column<byte>(type: "tinyint", nullable: false),
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HolidayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdHocHolidays", x => x.AdHocHolidayID);
                    table.ForeignKey(
                        name: "FK_AdHocHolidays_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_AdHocHolidays_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Alphabets",
                schema: "dbo",
                columns: table => new
                {
                    alphabet_id = table.Column<byte>(type: "tinyint", nullable: false),
                    alphabet_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alphabets", x => x.alphabet_id);
                    table.ForeignKey(
                        name: "FK_Alphabets_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Alphabets_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Ambulance",
                schema: "dbo",
                columns: table => new
                {
                    Ambulance_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amb_area = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Amb_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Amb_tel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Amb_fax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ambulance", x => x.Ambulance_code);
                    table.ForeignKey(
                        name: "FK_Ambulance_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Ambulance_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "anchor_types",
                schema: "dbo",
                columns: table => new
                {
                    anchor_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    anchor_type_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anchor_types", x => x.anchor_type_code);
                    table.ForeignKey(
                        name: "FK_anchor_types_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_anchor_types_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "dbo",
                columns: table => new
                {
                    AuditID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrimaryKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.AuditID);
                    table.ForeignKey(
                        name: "FK_Audit_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Audit_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "batch",
                schema: "dbo",
                columns: table => new
                {
                    batch_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    batch_turnover = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    batch_header_date = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    batch_header_time = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    financial_system_code = table.Column<byte>(type: "tinyint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch", x => x.batch_code);
                    table.ForeignKey(
                        name: "FK_batch_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_batch_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "bill_of_material",
                schema: "dbo",
                columns: table => new
                {
                    bom_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_of_material", x => x.bom_code);
                    table.ForeignKey(
                        name: "FK_bill_of_material_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bill_of_material_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "bitmask_def",
                schema: "dbo",
                columns: table => new
                {
                    function = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    mask = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bitmask_def", x => new { x.function, x.mask });
                    table.ForeignKey(
                        name: "FK_bitmask_def_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bitmask_def_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Booking_address",
                schema: "dbo",
                columns: table => new
                {
                    location_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    net_address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Booking_address", x => x.location_code);
                    table.ForeignKey(
                        name: "FK_Booking_address_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Booking_address_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "burn_rate",
                schema: "dbo",
                columns: table => new
                {
                    burn_rate_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    company_id = table.Column<short>(type: "smallint", nullable: true),
                    company_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    department_id = table.Column<short>(type: "smallint", nullable: true),
                    department_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    department_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_burn_rate", x => x.burn_rate_code);
                    table.ForeignKey(
                        name: "FK_burn_rate_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_burn_rate_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "class",
                schema: "dbo",
                columns: table => new
                {
                    class_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class", x => x.class_code);
                    table.ForeignKey(
                        name: "FK_class_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_class_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "company",
                schema: "dbo",
                columns: table => new
                {
                    company_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address_1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address_2 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Address_3 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Postal_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.company_code);
                    table.ForeignKey(
                        name: "FK_company_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_company_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "connection_type",
                schema: "dbo",
                columns: table => new
                {
                    connection_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_type", x => x.connection_id);
                    table.ForeignKey(
                        name: "FK_connection_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_connection_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "contract_status",
                schema: "dbo",
                columns: table => new
                {
                    contract_status_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    status_description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    status_abbreviation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    is_final = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_status", x => x.contract_status_code);
                    table.ForeignKey(
                        name: "FK_contract_status_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_contract_status_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Contract_type",
                schema: "dbo",
                columns: table => new
                {
                    contract_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CT_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CT_Active = table.Column<bool>(type: "bit", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract_type", x => x.contract_type);
                    table.ForeignKey(
                        name: "FK_Contract_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Contract_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Contract_Type_Group_Mapping",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vs_code = table.Column<int>(type: "int", nullable: false),
                    type_code = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract_Type_Group_Mapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contract_Type_Group_Mapping_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Contract_Type_Group_Mapping_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Contractors",
                schema: "dbo",
                columns: table => new
                {
                    contractor_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contractor_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    physical_address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    postal_address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    tel_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    fax_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.contractor_id);
                    table.ForeignKey(
                        name: "FK_Contractors_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Contractors_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "cost_category",
                schema: "dbo",
                columns: table => new
                {
                    cost_category_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    vat_recoverable = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    cpk_contribution = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    journal_detail_type_group_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_category", x => x.cost_category_code);
                    table.ForeignKey(
                        name: "FK_cost_category_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_cost_category_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "cost_revenue_map",
                schema: "dbo",
                columns: table => new
                {
                    cost_revenue_map_code = table.Column<byte>(type: "tinyint", nullable: false),
                    journal_detail_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    journal_detail_revenue_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    journal_detail_revenue_type_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_revenue_map", x => x.cost_revenue_map_code);
                    table.ForeignKey(
                        name: "FK_cost_revenue_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_cost_revenue_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "daily_import_except",
                schema: "dbo",
                columns: table => new
                {
                    except_ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PAN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    reg_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    voucher = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    import_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    exception_desc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_import_except", x => x.except_ID);
                    table.ForeignKey(
                        name: "FK_daily_import_except_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_daily_import_except_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "db_ddl_log",
                schema: "dbo",
                columns: table => new
                {
                    db_ddl_log_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    database_user = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    @event = table.Column<string>(name: "event", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    schema = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    @object = table.Column<string>(name: "object", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    tsql = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_db_ddl_log", x => x.db_ddl_log_code);
                    table.ForeignKey(
                        name: "FK_db_ddl_log_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_db_ddl_log_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "db_version",
                schema: "dbo",
                columns: table => new
                {
                    db_version_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    db_version_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    db_version_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_db_version", x => x.db_version_code);
                    table.ForeignKey(
                        name: "FK_db_version_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_db_version_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "driver_licence",
                schema: "dbo",
                columns: table => new
                {
                    licence_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_licence", x => x.licence_code);
                    table.ForeignKey(
                        name: "FK_driver_licence_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_driver_licence_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "driver_licence_types",
                schema: "dbo",
                columns: table => new
                {
                    driver_licence_type_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    driver_licence_type_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    driver_licence_type_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver_licence_types", x => x.driver_licence_type_id);
                    table.ForeignKey(
                        name: "FK_driver_licence_types_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_driver_licence_types_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "EduCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_Code = table.Column<int>(type: "int", nullable: true),
                    Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RespNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RespName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ObjNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ObjName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EduCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EduCodes_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_EduCodes_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "EntraId_User_Mapping",
                schema: "dbo",
                columns: table => new
                {
                    mapping_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    entra_object_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    user_access_code = table.Column<int>(type: "int", nullable: false),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntraId_User_Mapping", x => x.mapping_id);
                    table.ForeignKey(
                        name: "FK_EntraId_User_Mapping_User",
                        column: x => x.user_access_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "error_log",
                schema: "dbo",
                columns: table => new
                {
                    error_log_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    log_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    log_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    log_read = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_log", x => x.error_log_code);
                    table.ForeignKey(
                        name: "FK_error_log_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_error_log_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "EventMap",
                schema: "dbo",
                columns: table => new
                {
                    EventMapID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    WorkflowID = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventMap", x => x.EventMapID);
                    table.ForeignKey(
                        name: "FK_EventMap_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_EventMap_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "extra_codes",
                schema: "dbo",
                columns: table => new
                {
                    extra_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    extra_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    category_type_code = table.Column<int>(type: "int", nullable: true),
                    specific = table.Column<int>(type: "int", nullable: true),
                    Additional = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extra_codes", x => x.extra_code);
                    table.ForeignKey(
                        name: "FK_extra_codes_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_extra_codes_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "financial_system",
                schema: "dbo",
                columns: table => new
                {
                    financial_system_code = table.Column<byte>(type: "tinyint", nullable: false),
                    financial_system_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_system", x => x.financial_system_code);
                    table.ForeignKey(
                        name: "FK_financial_system_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_financial_system_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "financial_year",
                schema: "dbo",
                columns: table => new
                {
                    financial_year_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    financial_year = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_year", x => x.financial_year_code);
                    table.ForeignKey(
                        name: "FK_financial_year_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_financial_year_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "fuel_recovery_configuration",
                schema: "dbo",
                columns: table => new
                {
                    fuel_recovery_configuration_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    recovery_percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_recovery_configuration", x => x.fuel_recovery_configuration_code);
                    table.ForeignKey(
                        name: "FK_fuel_recovery_configuration_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fuel_recovery_configuration_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "fuel_tariff",
                schema: "dbo",
                columns: table => new
                {
                    fuel_tariff_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fuel_type_code = table.Column<short>(type: "smallint", nullable: false),
                    fuel_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    fuel_tariff_notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_tariff", x => x.fuel_tariff_code);
                    table.ForeignKey(
                        name: "FK_fuel_tariff_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fuel_tariff_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "GG_Block",
                schema: "dbo",
                columns: table => new
                {
                    Block_ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Creation_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Created_By_User_Code = table.Column<short>(type: "smallint", nullable: false),
                    Vch_Start_Reg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Vch_End_Reg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Modified_User_Code = table.Column<short>(type: "smallint", nullable: false),
                    audit_date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    audit_date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    audit_created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    audit_modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GG_Block", x => x.Block_ID);
                    table.ForeignKey(
                        name: "FK_GG_Block_TS_Users_audit_created_by_user_code",
                        column: x => x.audit_created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_GG_Block_TS_Users_audit_modified_by_user_code",
                        column: x => x.audit_modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Incident_Area",
                schema: "dbo",
                columns: table => new
                {
                    Incident_area_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Town_SubArea = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incident_Area", x => x.Incident_area_code);
                    table.ForeignKey(
                        name: "FK_Incident_Area_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Incident_Area_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Income_Split_TempTable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    source_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionFinYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    journal_detail_id = table.Column<int>(type: "int", nullable: true),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    journal_code = table.Column<long>(type: "bigint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Income_Split_TempTable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Income_Split_TempTable_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Income_Split_TempTable_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "invoice",
                schema: "dbo",
                columns: table => new
                {
                    invoice_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    posting_month_code = table.Column<short>(type: "smallint", nullable: false),
                    department_code = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice", x => x.invoice_code);
                    table.ForeignKey(
                        name: "FK_invoice_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_invoice_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "journal_detail_type",
                schema: "dbo",
                columns: table => new
                {
                    journal_detail_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    journal_detail_type_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    journal_detail_type_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    journal_detail_type_isggmt = table.Column<bool>(type: "bit", nullable: false),
                    journal_detail_type_isreversal = table.Column<bool>(type: "bit", nullable: false),
                    journal_detail_type_issuspense = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_detail_type", x => x.journal_detail_type_code);
                    table.ForeignKey(
                        name: "FK_journal_detail_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_detail_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "journal_detail_type_group",
                schema: "dbo",
                columns: table => new
                {
                    journal_detail_type_group_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    journal_detail_type_group_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_detail_type_group", x => x.journal_detail_type_group_code);
                    table.ForeignKey(
                        name: "FK_journal_detail_type_group_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_detail_type_group_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Journal_WithInvalidBasCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GGNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BasCode_FinancialYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Enter_Correct_Responsibility_Number_Only = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Enter_Correct_Objective_Number_Only = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SiteName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journal_WithInvalidBasCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journal_WithInvalidBasCodes_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Journal_WithInvalidBasCodes_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Legacy_User_Credentials",
                schema: "dbo",
                columns: table => new
                {
                    credential_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_access_code = table.Column<int>(type: "int", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    password_salt = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    password_reset_token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    password_reset_token_expiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    account_locked_until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_password_change = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    modified_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Legacy_User_Credentials", x => x.credential_id);
                    table.ForeignKey(
                        name: "FK_Legacy_User_Credentials_User",
                        column: x => x.user_access_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "licence_fee",
                schema: "dbo",
                columns: table => new
                {
                    licence_fee_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    licence_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    licence_fee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licence_fee", x => x.licence_fee_code);
                    table.ForeignKey(
                        name: "FK_licence_fee_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_licence_fee_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "license",
                schema: "dbo",
                columns: table => new
                {
                    licence_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    licence_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    licence_category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license", x => x.licence_code);
                    table.ForeignKey(
                        name: "FK_license_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_license_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "dbo",
                columns: table => new
                {
                    LocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Province = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_Locations_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Locations_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Loss_type",
                schema: "dbo",
                columns: table => new
                {
                    loss_type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    loss_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loss_type", x => x.loss_type_code);
                    table.ForeignKey(
                        name: "FK_Loss_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Loss_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "maint_trigger_type",
                schema: "dbo",
                columns: table => new
                {
                    maint_trigger_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maint_trigger_type", x => x.maint_trigger_type_code);
                    table.ForeignKey(
                        name: "FK_maint_trigger_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_maint_trigger_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Maintenance_Value_History",
                schema: "dbo",
                columns: table => new
                {
                    Maintenance_Value_History_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffParameterID = table.Column<int>(type: "int", nullable: false),
                    class_code = table.Column<short>(type: "smallint", nullable: false),
                    months_age = table.Column<short>(type: "smallint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maintenance_Value_History", x => x.Maintenance_Value_History_ID);
                    table.ForeignKey(
                        name: "FK_Maintenance_Value_History_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Maintenance_Value_History_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "make",
                schema: "dbo",
                columns: table => new
                {
                    make_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    make_description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_make", x => x.make_code);
                    table.ForeignKey(
                        name: "FK_make_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_make_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Monthly_burn_rate",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    month = table.Column<int>(type: "int", nullable: true),
                    year = table.Column<int>(type: "int", nullable: true),
                    department_number = table.Column<int>(type: "int", nullable: true),
                    Fixed_income_total = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Fixed_cost_total = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    percent_Replacement = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monthly_burn_rate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Monthly_burn_rate_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Monthly_burn_rate_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Monthly_pool_vehicles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    month = table.Column<int>(type: "int", nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    site_code = table.Column<int>(type: "int", nullable: false),
                    pool_vehicles = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monthly_pool_vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Monthly_pool_vehicles_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Monthly_pool_vehicles_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "New_Vehicles_received",
                schema: "dbo",
                columns: table => new
                {
                    new_vehicle_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<short>(type: "smallint", nullable: false),
                    gg_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    engine_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    chassis_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    extras = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_New_Vehicles_received", x => x.new_vehicle_code);
                    table.ForeignKey(
                        name: "FK_New_Vehicles_received_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_New_Vehicles_received_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "OverheadType",
                schema: "dbo",
                columns: table => new
                {
                    OverheadTypeId = table.Column<byte>(type: "tinyint", nullable: false),
                    OverheadTypeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverheadType", x => x.OverheadTypeId);
                    table.ForeignKey(
                        name: "FK_OverheadType_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_OverheadType_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "overtime_multiplier",
                schema: "dbo",
                columns: table => new
                {
                    overtime_multiplier_code = table.Column<byte>(type: "tinyint", nullable: false),
                    overtime_multiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overtime_multiplier", x => x.overtime_multiplier_code);
                    table.ForeignKey(
                        name: "FK_overtime_multiplier_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_overtime_multiplier_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "part",
                schema: "dbo",
                columns: table => new
                {
                    part_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    bom_code = table.Column<int>(type: "int", nullable: true),
                    part_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    qty_on_hand = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    qty_on_order = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_part", x => x.part_code);
                    table.ForeignKey(
                        name: "FK_part_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_part_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "PastelCustomer",
                schema: "dbo",
                columns: table => new
                {
                    pcID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    customerID = table.Column<int>(type: "int", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    department_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastelCustomer", x => x.pcID);
                    table.ForeignKey(
                        name: "FK_PastelCustomer_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PastelCustomer_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "PastelGL",
                schema: "dbo",
                columns: table => new
                {
                    GLID = table.Column<byte>(type: "tinyint", nullable: false),
                    account = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    accountLink = table.Column<int>(type: "int", nullable: true),
                    accountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastelGL", x => x.GLID);
                    table.ForeignKey(
                        name: "FK_PastelGL_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PastelGL_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "PastelStatic",
                schema: "dbo",
                columns: table => new
                {
                    staticID = table.Column<byte>(type: "tinyint", nullable: false),
                    idLinePermanent = table.Column<int>(type: "int", nullable: true),
                    iValidateFlag = table.Column<int>(type: "int", nullable: true),
                    iAccountCurrencyID = table.Column<int>(type: "int", nullable: true),
                    cAccountCurrencySymbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    bTrCodeHasTax = table.Column<bool>(type: "bit", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastelStatic", x => x.staticID);
                    table.ForeignKey(
                        name: "FK_PastelStatic_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PastelStatic_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "dbo",
                columns: table => new
                {
                    Position_Code = table.Column<byte>(type: "tinyint", nullable: false),
                    Position_Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Position_Code);
                    table.ForeignKey(
                        name: "FK_Positions_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Positions_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "posting_month",
                schema: "dbo",
                columns: table => new
                {
                    posting_month_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    posting_year_code = table.Column<short>(type: "smallint", nullable: false),
                    month_number = table.Column<byte>(type: "tinyint", nullable: false),
                    month_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    is_closed = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_month", x => x.posting_month_code);
                    table.ForeignKey(
                        name: "FK_posting_month_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_posting_month_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "pre_vehicle_master",
                schema: "dbo",
                columns: table => new
                {
                    temp_vmf_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    registration_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pre_vehicle_master", x => x.temp_vmf_code);
                    table.ForeignKey(
                        name: "FK_pre_vehicle_master_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_pre_vehicle_master_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Private_hire",
                schema: "dbo",
                columns: table => new
                {
                    PHV_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    registration_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    site_code = table.Column<int>(type: "int", nullable: false),
                    contracted_to = table.Column<int>(type: "int", nullable: true),
                    engine_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    chassis_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    year_manufactured = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bank_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    colour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tank_capacity = table.Column<int>(type: "int", nullable: true),
                    contractor_id = table.Column<short>(type: "smallint", nullable: false),
                    fuel_card = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fuel_card_receiver = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    take_on_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    take_on_odo = table.Column<int>(type: "int", nullable: false),
                    return_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    return_odo = table.Column<int>(type: "int", nullable: false),
                    km_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    daily_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    hourly_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    model_desc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Private_hire", x => x.PHV_code);
                    table.ForeignKey(
                        name: "FK_Private_hire_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Private_hire_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "province",
                schema: "dbo",
                columns: table => new
                {
                    province_code = table.Column<byte>(type: "tinyint", nullable: false),
                    province_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    province_abbreviation = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_province", x => x.province_code);
                    table.ForeignKey(
                        name: "FK_province_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_province_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "purchase_category",
                schema: "dbo",
                columns: table => new
                {
                    purchase_category_code = table.Column<byte>(type: "tinyint", nullable: false),
                    purchase_category_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_category", x => x.purchase_category_code);
                    table.ForeignKey(
                        name: "FK_purchase_category_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_purchase_category_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "ranks",
                schema: "dbo",
                columns: table => new
                {
                    rank_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranks", x => x.rank_code);
                    table.ForeignKey(
                        name: "FK_ranks_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_ranks_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "RT57",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ITEMNUMBER = table.Column<string>(name: "ITEM NUMBER", type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ITEMDESCRIPTION = table.Column<string>(name: "ITEM DESCRIPTION", type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BRAND = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PRICE = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CLASS = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<byte>(type: "tinyint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RT57", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RT57_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_RT57_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "segment_type",
                schema: "dbo",
                columns: table => new
                {
                    segment_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    segment_type_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segment_type", x => x.segment_type_code);
                    table.ForeignKey(
                        name: "FK_segment_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "site",
                schema: "dbo",
                columns: table => new
                {
                    Site_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Depatrment_code = table.Column<short>(type: "smallint", nullable: true),
                    description = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    res_person = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    postal_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    net_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Map_reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Map_description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cell_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    site_active = table.Column<bool>(type: "bit", nullable: false),
                    telephone2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    financial_system_code = table.Column<byte>(type: "tinyint", nullable: true),
                    financial_system_active = table.Column<bool>(type: "bit", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site", x => x.Site_code);
                    table.ForeignKey(
                        name: "FK_site_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_site_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "site_drivers",
                schema: "dbo",
                columns: table => new
                {
                    site_driver_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    site_code = table.Column<int>(type: "int", nullable: false),
                    driver_licence_type_id = table.Column<int>(type: "int", nullable: false),
                    driver_surname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_firstname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_SA_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_passportnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_persalnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_contractnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_licence_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_licence_issuedate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    driver_licence_lastVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    driver_hasPDP = table.Column<bool>(type: "bit", nullable: false),
                    driver_PDP_ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_licence_ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_active = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_drivers", x => x.site_driver_code);
                    table.ForeignKey(
                        name: "FK_site_drivers_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_site_drivers_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Status",
                schema: "Workflow",
                columns: table => new
                {
                    StatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepID = table.Column<int>(type: "int", nullable: false),
                    DateCompleted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DateStarted = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsBusy = table.Column<bool>(type: "bit", nullable: false),
                    StartedByUserName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.StatusID);
                    table.ForeignKey(
                        name: "FK_Status_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Status_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Step",
                schema: "Workflow",
                columns: table => new
                {
                    StepID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepTypeID = table.Column<int>(type: "int", nullable: false),
                    WorkflowID = table.Column<int>(type: "int", nullable: false),
                    ParentStepID = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Step", x => x.StepID);
                    table.ForeignKey(
                        name: "FK_Step_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Step_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "StepType",
                schema: "Workflow",
                columns: table => new
                {
                    StepTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StepTypeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StepTypeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepType", x => x.StepTypeID);
                    table.ForeignKey(
                        name: "FK_StepType_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_StepType_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                schema: "dbo",
                columns: table => new
                {
                    supplier_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    contact_person = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    supplier_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    active = table.Column<bool>(type: "bit", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.supplier_id);
                    table.ForeignKey(
                        name: "FK_Suppliers_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Suppliers_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "tally",
                schema: "dbo",
                columns: table => new
                {
                    tally_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tally_value = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tally", x => x.tally_code);
                    table.ForeignKey(
                        name: "FK_tally_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_tally_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "TariffParameter",
                schema: "fin",
                columns: table => new
                {
                    TariffParameterID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffParameterYear = table.Column<int>(type: "int", nullable: false),
                    AnnualInterestRatePercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualPayments = table.Column<byte>(type: "tinyint", nullable: false),
                    EffectiveInterestRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PoolVehicleChargedDaysPerMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    CostCategoryMultiple = table.Column<int>(type: "int", nullable: false),
                    AnnualRecoveredKilos = table.Column<int>(type: "int", nullable: true),
                    AverageFuelPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CaptureDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    user_access_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Approval_user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    Approval_user_access_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffParameter", x => x.TariffParameterID);
                    table.ForeignKey(
                        name: "FK_TariffParameter_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TariffParameter_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "task",
                schema: "dbo",
                columns: table => new
                {
                    task_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    profile_code = table.Column<short>(type: "smallint", nullable: false),
                    bom_code = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    duration_hours = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task", x => x.task_code);
                    table.ForeignKey(
                        name: "FK_task_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_task_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Temp_Vehicle_extras",
                schema: "dbo",
                columns: table => new
                {
                    extras_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    extra_code = table.Column<short>(type: "smallint", nullable: false),
                    quantity = table.Column<short>(type: "smallint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Temp_Vehicle_extras", x => x.extras_code);
                    table.ForeignKey(
                        name: "FK_Temp_Vehicle_extras_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Temp_Vehicle_extras_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "third_party_vehicle_model_description",
                schema: "dbo",
                columns: table => new
                {
                    vmf_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    model_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_third_party_vehicle_model_description", x => x.vmf_code);
                    table.ForeignKey(
                        name: "FK_third_party_vehicle_model_description_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_third_party_vehicle_model_description_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Tow_Truck",
                schema: "dbo",
                columns: table => new
                {
                    Tow_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tow_area = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Tow_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Tow_tel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tow_fax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tow_Truck", x => x.Tow_code);
                    table.ForeignKey(
                        name: "FK_Tow_Truck_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Tow_Truck_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Traffic_Dept",
                schema: "dbo",
                columns: table => new
                {
                    Traffic_dept_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Traf_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_res_person = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_post_address1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_post_address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_post_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Traf_email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traffic_Dept", x => x.Traffic_dept_code);
                    table.ForeignKey(
                        name: "FK_Traffic_Dept_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Traffic_Dept_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "trip_driver",
                schema: "dbo",
                columns: table => new
                {
                    trip_driver_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_driver_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    trip_driver_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    trip_authority_code = table.Column<int>(type: "int", nullable: false),
                    trip_driver_primary = table.Column<bool>(type: "bit", nullable: false),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    driver_licence_type_id = table.Column<int>(type: "int", nullable: true),
                    driver_passportnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_persalnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_contractnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_licence_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_licence_issuedate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_licence_lastVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_hasPDP = table.Column<bool>(type: "bit", nullable: false),
                    driver_PDP_ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_licence_ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_active = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_driver", x => x.trip_driver_code);
                    table.ForeignKey(
                        name: "FK_trip_driver_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_driver_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "trip_incident_type",
                schema: "dbo",
                columns: table => new
                {
                    trip_incident_type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_incident_type_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_incident_type", x => x.trip_incident_type_code);
                    table.ForeignKey(
                        name: "FK_trip_incident_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_incident_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "trip_passengers",
                schema: "dbo",
                columns: table => new
                {
                    trip_passenger_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_passenger_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_passengers", x => x.trip_passenger_code);
                    table.ForeignKey(
                        name: "FK_trip_passengers_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_passengers_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "trip_type",
                schema: "dbo",
                columns: table => new
                {
                    trip_type_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_type_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_type", x => x.trip_type_code);
                    table.ForeignKey(
                        name: "FK_trip_type_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_type_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "TS_Log",
                schema: "dbo",
                columns: table => new
                {
                    ErrorID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TSDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TSTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    SiteCode = table.Column<short>(type: "smallint", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Log", x => x.ErrorID);
                    table.ForeignKey(
                        name: "FK_TS_Log_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TS_Log_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "unit_of_measure",
                schema: "dbo",
                columns: table => new
                {
                    unit_of_measure_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    unit_description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    unit_abbreviation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    unit_category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_of_measure", x => x.unit_of_measure_code);
                    table.ForeignKey(
                        name: "FK_unit_of_measure_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_unit_of_measure_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "univ",
                schema: "dbo",
                columns: table => new
                {
                    univ_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    univ_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_univ", x => x.univ_code);
                    table.ForeignKey(
                        name: "FK_univ_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_univ_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "user_message",
                schema: "dbo",
                columns: table => new
                {
                    user_message_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_access_code = table.Column<int>(type: "int", nullable: false),
                    message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    message_read = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_message", x => x.user_message_code);
                    table.ForeignKey(
                        name: "FK_user_message_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_user_message_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_user_message_TS_Users_user_access_code",
                        column: x => x.user_access_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicle_orders",
                schema: "dbo",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    make_code = table.Column<short>(type: "smallint", nullable: true),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    quantity = table.Column<short>(type: "smallint", nullable: false),
                    supplier_id = table.Column<short>(type: "smallint", nullable: false),
                    order_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicle_orders", x => x.order_id);
                    table.ForeignKey(
                        name: "FK_Vehicle_orders_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Vehicle_orders_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_source",
                schema: "dbo",
                columns: table => new
                {
                    vs_code = table.Column<byte>(type: "tinyint", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    physical_address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    postal_address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    tel_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    fax_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_source", x => x.vs_code);
                    table.ForeignKey(
                        name: "FK_vehicle_source_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_source_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_status",
                schema: "dbo",
                columns: table => new
                {
                    vehicle_status_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    status_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status_predecessors = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    user_roles = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_status", x => x.vehicle_status_code);
                    table.ForeignKey(
                        name: "FK_vehicle_status_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_status_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "VehiclePhotoInfo",
                schema: "dbo",
                columns: table => new
                {
                    VehiclePhotoInfoCode = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleMasterCode = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Orientation = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePhotoInfo", x => x.VehiclePhotoInfoCode);
                    table.ForeignKey(
                        name: "FK_VehiclePhotoInfo_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_VehiclePhotoInfo_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "version",
                schema: "dbo",
                columns: table => new
                {
                    version_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    version_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    version_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_version", x => x.version_code);
                    table.ForeignKey(
                        name: "FK_version_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_version_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Wesbank_KilosPerFuelLitre",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    registration_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    kilos_per_litre = table.Column<double>(type: "float", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wesbank_KilosPerFuelLitre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wesbank_KilosPerFuelLitre_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Wesbank_KilosPerFuelLitre_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Workflow",
                schema: "Workflow",
                columns: table => new
                {
                    WorkflowID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AlwaysExecute = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflow", x => x.WorkflowID);
                    table.ForeignKey(
                        name: "FK_Workflow_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Workflow_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "wwmerchant",
                schema: "dbo",
                columns: table => new
                {
                    wwmerch_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    wwmerch_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    wwmerch_tel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    wwmerch_fax = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    wwmerch_email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wwmerchant", x => x.wwmerch_code);
                    table.ForeignKey(
                        name: "FK_wwmerchant_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_wwmerchant_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "batch_export",
                schema: "dbo",
                columns: table => new
                {
                    batch_export_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_code = table.Column<int>(type: "int", nullable: false),
                    batch_export_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    batch_export_turnover = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    department_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_export", x => x.batch_export_code);
                    table.ForeignKey(
                        name: "FK_batch_export_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_batch_export_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_batch_export_batch_batch_code",
                        column: x => x.batch_code,
                        principalSchema: "dbo",
                        principalTable: "batch",
                        principalColumn: "batch_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal",
                schema: "dbo",
                columns: table => new
                {
                    journal_code = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    batch_code = table.Column<int>(type: "int", nullable: false),
                    journal_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    journal_installation_link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal", x => x.journal_code);
                    table.ForeignKey(
                        name: "FK_journal_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_batch_batch_code",
                        column: x => x.batch_code,
                        principalSchema: "dbo",
                        principalTable: "batch",
                        principalColumn: "batch_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contractor_taxi_class",
                schema: "dbo",
                columns: table => new
                {
                    class_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contractor_id = table.Column<short>(type: "smallint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    km_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    driver_per_hour = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    daily_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractor_taxi_class", x => x.class_id);
                    table.ForeignKey(
                        name: "FK_Contractor_taxi_class_Contractors_contractor_id",
                        column: x => x.contractor_id,
                        principalSchema: "dbo",
                        principalTable: "Contractors",
                        principalColumn: "contractor_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Contractor_taxi_class_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Contractor_taxi_class_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "block_gg_numbers",
                schema: "dbo",
                columns: table => new
                {
                    Block_GEN_ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Block_ID = table.Column<short>(type: "smallint", nullable: false),
                    Creation_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Created_By_User_Code = table.Column<short>(type: "smallint", nullable: false),
                    GG_Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    audit_date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    audit_date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    audit_created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    audit_modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_block_gg_numbers", x => x.Block_GEN_ID);
                    table.ForeignKey(
                        name: "FK_block_gg_numbers_GG_Block_Block_ID",
                        column: x => x.Block_ID,
                        principalSchema: "dbo",
                        principalTable: "GG_Block",
                        principalColumn: "Block_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_block_gg_numbers_TS_Users_audit_created_by_user_code",
                        column: x => x.audit_created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_block_gg_numbers_TS_Users_audit_modified_by_user_code",
                        column: x => x.audit_modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "invoice_item",
                schema: "dbo",
                columns: table => new
                {
                    item_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_code = table.Column<int>(type: "int", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: false),
                    fixed_tariff_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    start_odometer = table.Column<int>(type: "int", nullable: false),
                    start_odo_derived = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    start_odo_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    end_odometer = table.Column<int>(type: "int", nullable: false),
                    end_odo_derived = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    end_odo_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    odo_tariff_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    driver_rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    contract_start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    contract_end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    contract_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    contract_start_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    contract_end_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cost_replacement = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    provision_overhead = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    cost_overhead = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    provision_loss = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    provision_accident = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    provision_profit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    provision_replacement = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    variable_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    invoice_code1 = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_item", x => x.item_code);
                    table.ForeignKey(
                        name: "FK_invoice_item_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_invoice_item_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_invoice_item_invoice_invoice_code1",
                        column: x => x.invoice_code1,
                        principalSchema: "dbo",
                        principalTable: "invoice",
                        principalColumn: "invoice_code");
                });

            migrationBuilder.CreateTable(
                name: "model",
                schema: "dbo",
                columns: table => new
                {
                    model_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    make_code = table.Column<short>(type: "smallint", nullable: false),
                    unit_of_measure_code = table.Column<short>(type: "smallint", nullable: false),
                    fuel_type_code = table.Column<short>(type: "smallint", nullable: false),
                    licence_code = table.Column<short>(type: "smallint", nullable: false),
                    maint_trigger_code = table.Column<short>(type: "smallint", nullable: true),
                    class_code = table.Column<short>(type: "smallint", nullable: false),
                    type_code = table.Column<short>(type: "smallint", nullable: true),
                    model_description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    engine_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    engine_capacity = table.Column<short>(type: "smallint", nullable: true),
                    rated_power = table.Column<short>(type: "smallint", nullable: true),
                    fuel_tank_capacity = table.Column<short>(type: "smallint", nullable: true),
                    target_consumption = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    target_tyre_life = table.Column<int>(type: "int", nullable: true),
                    service_interval = table.Column<int>(type: "int", nullable: true),
                    vemm_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    licence_fee_code = table.Column<short>(type: "smallint", nullable: true),
                    gvm = table.Column<int>(type: "int", nullable: true),
                    transmission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    wesbank_kilos_per_litre = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model", x => x.model_code);
                    table.ForeignKey(
                        name: "FK_Model_Make",
                        column: x => x.make_code,
                        principalSchema: "dbo",
                        principalTable: "make",
                        principalColumn: "make_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_model_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "PreVehicle_Master_note",
                schema: "dbo",
                columns: table => new
                {
                    Pre_Note_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    temp_vmf_code = table.Column<int>(type: "int", nullable: false),
                    Pre_Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pre_Note_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreVehicle_Master_note", x => x.Pre_Note_code);
                    table.ForeignKey(
                        name: "FK_PreVehicle_Master_note_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PreVehicle_Master_note_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PreVehicle_Master_note_pre_vehicle_master_temp_vmf_code",
                        column: x => x.temp_vmf_code,
                        principalSchema: "dbo",
                        principalTable: "pre_vehicle_master",
                        principalColumn: "temp_vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approvers",
                schema: "dbo",
                columns: table => new
                {
                    approver_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    department_code = table.Column<int>(type: "int", nullable: false),
                    rank_code = table.Column<int>(type: "int", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Firstname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approvers", x => x.approver_code);
                    table.ForeignKey(
                        name: "FK_approvers_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_approvers_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_approvers_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "bassegment",
                schema: "dbo",
                columns: table => new
                {
                    segment_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    segment_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    segment_group_code = table.Column<int>(type: "int", nullable: false),
                    department_code = table.Column<short>(type: "smallint", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bassegment", x => x.segment_code);
                    table.ForeignKey(
                        name: "FK_bassegment_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bassegment_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bassegment_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "Demo_vehicles",
                schema: "dbo",
                columns: table => new
                {
                    demo_vehicle_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    gg_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    reg_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    model_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    year_mnf = table.Column<int>(type: "int", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demo_vehicles", x => x.demo_vehicle_code);
                    table.ForeignKey(
                        name: "FK_Demo_vehicles_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Demo_vehicles_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Demo_vehicles_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "department",
                schema: "dbo",
                columns: table => new
                {
                    department_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    company_code = table.Column<short>(type: "smallint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    res_person = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    address3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    postal_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    net_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cell_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department_abbr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bas_installation_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    dept_active = table.Column<bool>(type: "bit", nullable: false),
                    clo_email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    telephone2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    financial_system_code = table.Column<byte>(type: "tinyint", nullable: true),
                    financial_system_active = table.Column<bool>(type: "bit", nullable: true),
                    financial_system_activate_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    default_site = table.Column<short>(type: "smallint", nullable: true),
                    export_is_active = table.Column<bool>(type: "bit", nullable: true),
                    date_last_exported = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Service_Kilometres = table.Column<int>(type: "int", nullable: false),
                    Service_Years = table.Column<byte>(type: "tinyint", nullable: false),
                    Overhead_Percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultSiteSite_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department", x => x.department_code);
                    table.ForeignKey(
                        name: "FK_department_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_department_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_department_site_DefaultSiteSite_code",
                        column: x => x.DefaultSiteSite_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceValue",
                schema: "fin",
                columns: table => new
                {
                    TariffParameterID = table.Column<int>(type: "int", nullable: false),
                    class_code = table.Column<short>(type: "smallint", nullable: false),
                    months_age = table.Column<short>(type: "smallint", nullable: false),
                    class_number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    kilometer_age = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RandPerKilometer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CaptureDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    user_access_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceValue", x => new { x.TariffParameterID, x.class_code, x.months_age });
                    table.ForeignKey(
                        name: "FK_MaintenanceValue_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_MaintenanceValue_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_MaintenanceValue_TariffParameter_TariffParameterID",
                        column: x => x.TariffParameterID,
                        principalSchema: "fin",
                        principalTable: "TariffParameter",
                        principalColumn: "TariffParameterID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Overhead",
                schema: "fin",
                columns: table => new
                {
                    OverheadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OverheadDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverheadAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OverheadTypeId = table.Column<byte>(type: "tinyint", nullable: false),
                    OverheadNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TariffParameterID = table.Column<int>(type: "int", nullable: false),
                    CaptureDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    user_access_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Overhead", x => x.OverheadId);
                    table.ForeignKey(
                        name: "FK_Overhead_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Overhead_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Overhead_TariffParameter_TariffParameterID",
                        column: x => x.TariffParameterID,
                        principalSchema: "fin",
                        principalTable: "TariffParameter",
                        principalColumn: "TariffParameterID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TariffWeightCalculation",
                schema: "fin",
                columns: table => new
                {
                    TariffWeightCalculation_Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TariffParameterID = table.Column<int>(type: "int", nullable: false),
                    category = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    number = table.Column<int>(type: "int", nullable: true),
                    WeightFactorPerUnit = table.Column<double>(type: "float", nullable: true),
                    calculation_date_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffWeightCalculation", x => x.TariffWeightCalculation_Code);
                    table.ForeignKey(
                        name: "FK_TariffWeightCalculation_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TariffWeightCalculation_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TariffWeightCalculation_TariffParameter_TariffParameterID",
                        column: x => x.TariffParameterID,
                        principalSchema: "fin",
                        principalTable: "TariffParameter",
                        principalColumn: "TariffParameterID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_tariff",
                schema: "fin",
                columns: table => new
                {
                    vehicle_tariff_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    residual_percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    parameter_year = table.Column<short>(type: "smallint", nullable: false),
                    annual_interest_percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    purchase_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    purchase_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    purchase_amount_group = table.Column<byte>(type: "tinyint", nullable: true),
                    overhead_unit_factor = table.Column<double>(type: "float", nullable: true),
                    target_replacement_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    year_manufactured = table.Column<int>(type: "int", nullable: true),
                    model_code = table.Column<int>(type: "int", nullable: true),
                    class_code = table.Column<int>(type: "int", nullable: true),
                    kilometer_life = table.Column<int>(type: "int", nullable: true),
                    months_life = table.Column<byte>(type: "tinyint", nullable: true),
                    residual_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    capital_payment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    overhead_payment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    adjustment_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    vehicle_fixed_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    vehicle_fixed_daily_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    vehicle_fixed_tariff_pool = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    class_fixed_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    class_fixed_pool_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    lease_fixed_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    overhead_kilometer_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    maintenance_kilometer_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    vehicle_kilometer_tariff = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    calculation_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    TariffParameterID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_tariff", x => x.vehicle_tariff_code);
                    table.ForeignKey(
                        name: "FK_vehicle_tariff_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_tariff_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_tariff_TariffParameter_TariffParameterID",
                        column: x => x.TariffParameterID,
                        principalSchema: "fin",
                        principalTable: "TariffParameter",
                        principalColumn: "TariffParameterID");
                });

            migrationBuilder.CreateTable(
                name: "maint_profile_model",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    profile_code = table.Column<short>(type: "smallint", nullable: false),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    trigger_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    trigger_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    interval = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maint_profile_model", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maint_profile_model_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_maint_profile_model_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_maint_profile_model_model_model_code",
                        column: x => x.model_code,
                        principalSchema: "dbo",
                        principalTable: "model",
                        principalColumn: "model_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Model_KilosPerFuelLitre",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    kilos_per_litre = table.Column<double>(type: "float", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Model_KilosPerFuelLitre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Model_KilosPerFuelLitre_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Model_KilosPerFuelLitre_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Model_KilosPerFuelLitre_model_model_code",
                        column: x => x.model_code,
                        principalSchema: "dbo",
                        principalTable: "model",
                        principalColumn: "model_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_master",
                schema: "dbo",
                columns: table => new
                {
                    vmf_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    model_code = table.Column<short>(type: "smallint", nullable: false),
                    type_code = table.Column<short>(type: "smallint", nullable: false),
                    vehicle_status_code = table.Column<short>(type: "smallint", nullable: false),
                    location_code = table.Column<short>(type: "smallint", nullable: false),
                    fleet_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    registration_number = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    take_on_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    take_on_odo = table.Column<int>(type: "int", nullable: false),
                    current_odo = table.Column<int>(type: "int", nullable: false),
                    odo_adjustment = table.Column<int>(type: "int", nullable: true),
                    derived_odo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    odo_update_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    engine_number_1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    chassis_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tare = table.Column<int>(type: "int", nullable: true),
                    gvm = table.Column<int>(type: "int", nullable: true),
                    year_manufactured = table.Column<short>(type: "smallint", nullable: true),
                    optional_extras = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    licence_due_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    additional_fuel_tank = table.Column<int>(type: "int", nullable: true),
                    average_consumption = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    fuel_card_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fuel_card_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    purchase_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    purchase_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    book_value = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    book_value_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    maint_card_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    maint_card_exdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    purchased_from = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sold_to = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sold_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    sold_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    service_last_done = table.Column<DateTime>(type: "datetime2", nullable: true),
                    service_last_odo = table.Column<int>(type: "int", nullable: true),
                    cof_last_done = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cof_required = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    cof_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    operator_card_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    monthly_overhead = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    colour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tow_hitch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    canopy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cof_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Licence_receiver = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Licence_receiver_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Licence_receiver_tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Licence_receiver_site = table.Column<short>(type: "smallint", nullable: true),
                    Licence_date_taken = table.Column<DateTime>(type: "datetime2", nullable: true),
                    highest_km = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    fuel_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    fuel_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    fuel_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    oil_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    oil_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    oil_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    maint_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    maint_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    maint_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    repairs_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    repairs_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    repairs_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    tyres_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    tyres_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    tyres_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    accident_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    accident_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    accident_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    toll_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    toll_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    toll_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    other_ltd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    other_ytd = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    other_3month_average = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    km_ltd = table.Column<int>(type: "int", nullable: true),
                    km_ytd = table.Column<int>(type: "int", nullable: true),
                    km_3month_average = table.Column<int>(type: "int", nullable: true),
                    lic_register_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    lic_registration_doc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    licence_comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    default_site = table.Column<short>(type: "smallint", nullable: true),
                    previos_gg_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    followup_gg_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    vehicle_status_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    renumbered_to = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    captured_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reserved = table.Column<short>(type: "smallint", nullable: true),
                    LPG = table.Column<bool>(type: "bit", nullable: true),
                    extended_service = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    destroyed_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    destroyed_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    destroyed_receipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    previos_gg_number_2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_First_Regist = table.Column<DateTime>(type: "datetime2", nullable: true),
                    vs_code = table.Column<byte>(type: "tinyint", nullable: true),
                    invoice_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelieveVehicle = table.Column<bool>(type: "bit", nullable: true),
                    initial_site_code = table.Column<short>(type: "smallint", nullable: true),
                    veh_site_code = table.Column<short>(type: "smallint", nullable: true),
                    temp_vmf_code = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Site_code = table.Column<short>(type: "smallint", nullable: true),
                    make_code = table.Column<short>(type: "smallint", nullable: true),
                    model_code1 = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_master", x => x.vmf_code);
                    table.CheckConstraint("CK_Vehicle_CurrentOdometer", "current_odo >= 0");
                    table.CheckConstraint("CK_Vehicle_PurchasePrice", "purchase_amount >= 0");
                    table.ForeignKey(
                        name: "FK_vehicle_master_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "Locations",
                        principalColumn: "LocationId");
                    table.ForeignKey(
                        name: "FK_vehicle_master_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_master_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_master_make_make_code",
                        column: x => x.make_code,
                        principalSchema: "dbo",
                        principalTable: "make",
                        principalColumn: "make_code");
                    table.ForeignKey(
                        name: "FK_vehicle_master_model_model_code1",
                        column: x => x.model_code1,
                        principalSchema: "dbo",
                        principalTable: "model",
                        principalColumn: "model_code");
                    table.ForeignKey(
                        name: "FK_vehicle_master_site_Site_code",
                        column: x => x.Site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "province_segment_map",
                schema: "dbo",
                columns: table => new
                {
                    Province_code = table.Column<byte>(type: "tinyint", nullable: false),
                    segment_code = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_province_segment_map", x => new { x.Province_code, x.segment_code });
                    table.ForeignKey(
                        name: "FK_province_segment_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_province_segment_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_province_segment_map_bassegment_segment_code",
                        column: x => x.segment_code,
                        principalSchema: "dbo",
                        principalTable: "bassegment",
                        principalColumn: "segment_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_province_segment_map_province_Province_code",
                        column: x => x.Province_code,
                        principalSchema: "dbo",
                        principalTable: "province",
                        principalColumn: "province_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Taxis",
                schema: "dbo",
                columns: table => new
                {
                    request_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rek_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    contractor_id = table.Column<short>(type: "smallint", nullable: true),
                    vmf_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    department_code = table.Column<short>(type: "smallint", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: false),
                    date_required = table.Column<DateTime>(type: "datetime2", nullable: false),
                    time_required = table.Column<DateTime>(type: "datetime2", nullable: false),
                    vehicle_type_code = table.Column<short>(type: "smallint", nullable: true),
                    official = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    rank = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    address_1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address_2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    address_3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    flight = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxis", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_Taxis_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Taxis_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Taxis_department_department_code",
                        column: x => x.department_code,
                        principalSchema: "dbo",
                        principalTable: "department",
                        principalColumn: "department_code");
                    table.ForeignKey(
                        name: "FK_Taxis_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accident",
                schema: "dbo",
                columns: table => new
                {
                    accident_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    posting_month_code = table.Column<short>(type: "smallint", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    driver_employ_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    hq_reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    gg_reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sa_reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    occurence_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    occurence_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reported_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    claim_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    excess_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accident", x => x.accident_code);
                    table.ForeignKey(
                        name: "FK_accident_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_accident_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_accident_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "anchor_points",
                schema: "dbo",
                columns: table => new
                {
                    anchor_point_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    anchor_odo_meter = table.Column<int>(type: "int", nullable: false),
                    anchor_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    anchor_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    bas_journal_record_code = table.Column<long>(type: "bigint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anchor_points", x => x.anchor_point_code);
                    table.ForeignKey(
                        name: "FK_anchor_points_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_anchor_points_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_anchor_points_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Asset_Verification",
                schema: "dbo",
                columns: table => new
                {
                    asset_verification_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    province = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    department_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    site_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    responsible_manager = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tel_no = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fax_no = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    vehicle_reg_no = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    verification_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    verified_by = table.Column<int>(type: "int", nullable: true),
                    verification_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asset_Verification", x => x.asset_verification_code);
                    table.ForeignKey(
                        name: "FK_Asset_Verification_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Asset_Verification_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Asset_Verification_TS_Users_verified_by",
                        column: x => x.verified_by,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Asset_Verification_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_Asset_Verification_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "auction",
                schema: "dbo",
                columns: table => new
                {
                    auction_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    auction_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    camp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    lot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    auction_garage = table.Column<short>(type: "smallint", nullable: true),
                    auth_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    auth_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    auction_km = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    garage_owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    reason_sold = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    estimate_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    reserve_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    sold_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    remark = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction", x => x.auction_code);
                    table.ForeignKey(
                        name: "FK_auction_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_auction_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_auction_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "dbo",
                columns: table => new
                {
                    booking_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    site_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    class_code = table.Column<short>(type: "smallint", nullable: false),
                    user_id = table.Column<short>(type: "smallint", nullable: false),
                    booking_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    collected = table.Column<short>(type: "smallint", nullable: true),
                    location_code = table.Column<int>(type: "int", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    booking_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.booking_id);
                    table.ForeignKey(
                        name: "FK_bookings_Locations_location_code",
                        column: x => x.location_code,
                        principalSchema: "dbo",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookings_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bookings_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_bookings_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "budget",
                schema: "dbo",
                columns: table => new
                {
                    budget_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    posting_year_code = table.Column<short>(type: "smallint", nullable: false),
                    annual_odo = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget", x => x.budget_code);
                    table.ForeignKey(
                        name: "FK_budget_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_budget_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_budget_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Call_centre",
                schema: "dbo",
                columns: table => new
                {
                    Call_centre_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    Call_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Call_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Capture_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    User_access_code = table.Column<short>(type: "smallint", nullable: true),
                    Caller_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Driver_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Driver_persalno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Driver_Licno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GG_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Driver_base_station = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Driver_Site = table.Column<short>(type: "smallint", nullable: true),
                    Driver_tel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Driver_cell = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Call_centre", x => x.Call_centre_code);
                    table.ForeignKey(
                        name: "FK_Call_centre_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Call_centre_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Call_centre_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "clearance",
                schema: "dbo",
                columns: table => new
                {
                    clearance_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    clearance_number = table.Column<int>(type: "int", nullable: true),
                    Clearance_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Merchant_code = table.Column<int>(type: "int", nullable: true),
                    Clearance_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    clearance_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    clearance_kilo = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clearance", x => x.clearance_code);
                    table.ForeignKey(
                        name: "FK_clearance_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_clearance_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_clearance_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collection",
                schema: "dbo",
                columns: table => new
                {
                    Collection_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sessionid = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    fleet_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collection", x => x.Collection_code);
                    table.ForeignKey(
                        name: "FK_Collection_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Collection_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Collection_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "contract",
                schema: "dbo",
                columns: table => new
                {
                    contract_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    start_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    end_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    start_odometer = table.Column<int>(type: "int", nullable: false),
                    end_odometer = table.Column<int>(type: "int", nullable: true),
                    still_current = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    contract_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Driver_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Authorisation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Driver_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    target_return_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_code = table.Column<short>(type: "smallint", nullable: true),
                    Charged_Until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    bas_objective_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bas_responsibility_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    relief_for_contract = table.Column<int>(type: "int", nullable: true),
                    locked_for_transfer = table.Column<bool>(type: "bit", nullable: false),
                    hours_used = table.Column<short>(type: "smallint", nullable: true),
                    bas_project_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    parent_contract_code = table.Column<int>(type: "int", nullable: true),
                    contract_group_code = table.Column<int>(type: "int", nullable: true),
                    bas_fund_code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    monthly_km = table.Column<int>(type: "int", nullable: true),
                    contract_status_code = table.Column<short>(type: "smallint", nullable: true),
                    contract_status_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    vehicle_assessment_code = table.Column<int>(type: "int", nullable: true),
                    approver_code = table.Column<int>(type: "int", nullable: true),
                    site_driver_code = table.Column<int>(type: "int", nullable: true),
                    collector_firstname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    ContractStatuscontract_status_code = table.Column<short>(type: "smallint", nullable: true),
                    department_code = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract", x => x.contract_code);
                    table.CheckConstraint("CK_Contract_Dates", "end_date IS NULL OR end_date > start_date");
                    table.CheckConstraint("CK_Contract_EndOdometer", "end_odometer IS NULL OR end_odometer > start_odometer");
                    table.CheckConstraint("CK_Contract_StartOdometer", "start_odometer >= 0");
                    table.CheckConstraint("CK_Contract_StillCurrent", "still_current IN ('Y', 'N')");
                    table.ForeignKey(
                        name: "FK_contract_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_contract_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_contract_contract_status_ContractStatuscontract_status_code",
                        column: x => x.ContractStatuscontract_status_code,
                        principalSchema: "dbo",
                        principalTable: "contract_status",
                        principalColumn: "contract_status_code");
                    table.ForeignKey(
                        name: "FK_contract_department_department_code",
                        column: x => x.department_code,
                        principalSchema: "dbo",
                        principalTable: "department",
                        principalColumn: "department_code");
                    table.ForeignKey(
                        name: "FK_contract_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_transactions",
                schema: "dbo",
                columns: table => new
                {
                    daily_transaction_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    cost_category_code = table.Column<short>(type: "smallint", nullable: false),
                    posting_month_code = table.Column<short>(type: "smallint", nullable: true),
                    file_sequence_number = table.Column<short>(type: "smallint", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_transactions", x => x.daily_transaction_code);
                    table.ForeignKey(
                        name: "FK_daily_transactions_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_daily_transactions_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_daily_transactions_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnjinNumbers",
                schema: "dbo",
                columns: table => new
                {
                    EnjinNumberID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    EnjinNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnjinNumbers", x => x.EnjinNumberID);
                    table.ForeignKey(
                        name: "FK_EnjinNumbers_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_EnjinNumbers_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_EnjinNumbers_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extras",
                schema: "dbo",
                columns: table => new
                {
                    extras_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    extra_code = table.Column<short>(type: "smallint", nullable: false),
                    quantity = table.Column<short>(type: "smallint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    serial_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extras", x => x.extras_code);
                    table.ForeignKey(
                        name: "FK_extras_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_extras_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_extras_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fines",
                schema: "dbo",
                columns: table => new
                {
                    Fine_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    Offence_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Offence_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Offence_issuer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Fine_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Appear_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Receive_gg_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notify_dept_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Site_code = table.Column<short>(type: "smallint", nullable: true),
                    Offence_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Fine_pay_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Withdraw_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Pay_due_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Issuer_notify_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fines", x => x.Fine_code);
                    table.ForeignKey(
                        name: "FK_Fines_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Fines_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Fines_site_Site_code",
                        column: x => x.Site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_Fines_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "fleet_notes",
                schema: "dbo",
                columns: table => new
                {
                    fleet_notes_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    fleet_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleet_notes", x => x.fleet_notes_code);
                    table.ForeignKey(
                        name: "FK_fleet_notes_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fleet_notes_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_fleet_notes_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fuel_card",
                schema: "dbo",
                columns: table => new
                {
                    Fuel_card_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    Counter = table.Column<short>(type: "smallint", nullable: true),
                    card_number = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    PAN_number = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    PetReceiver = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PetRecTel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PetTaken = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PetExpire = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PetComment = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LinkGGNum = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Status_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PetRecId = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    PetRecFax = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Bank_cnt = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Inciddat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Petrecsite = table.Column<short>(type: "smallint", nullable: true),
                    Petprint = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Garage = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fuel_card", x => x.Fuel_card_code);
                    table.ForeignKey(
                        name: "FK_Fuel_card_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Fuel_card_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Fuel_card_site_Petrecsite",
                        column: x => x.Petrecsite,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_Fuel_card_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "LeaseContractTerms",
                schema: "dbo",
                columns: table => new
                {
                    VehicleContractTermID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_Code = table.Column<int>(type: "int", nullable: false),
                    AgreedTerms = table.Column<int>(type: "int", nullable: true),
                    AgreedKilos = table.Column<long>(type: "bigint", nullable: true),
                    AppliedInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FixedMonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AuthorityStatus = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseContractTerms", x => x.VehicleContractTermID);
                    table.ForeignKey(
                        name: "FK_LeaseContractTerms_TS_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseContractTerms_TS_Users_ModifiedBy",
                        column: x => x.ModifiedBy,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseContractTerms_vehicle_master_vmf_Code",
                        column: x => x.vmf_Code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "logbook",
                schema: "dbo",
                columns: table => new
                {
                    logbookcode = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    begin_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    end_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    handout_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    lb_receiver_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    lb_tel_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    lb_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logbook", x => x.logbookcode);
                    table.ForeignKey(
                        name: "FK_logbook_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_logbook_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_logbook_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_logbook_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "Logsheets",
                schema: "dbo",
                columns: table => new
                {
                    log_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    start_odo = table.Column<double>(type: "float", nullable: false),
                    end_odo = table.Column<double>(type: "float", nullable: false),
                    month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: false),
                    rek_num = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    days_used = table.Column<int>(type: "int", nullable: true),
                    bund_num = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logsheets", x => x.log_code);
                    table.ForeignKey(
                        name: "FK_Logsheets_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Logsheets_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Logsheets_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Logsheets_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "losses",
                schema: "dbo",
                columns: table => new
                {
                    loss_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    loss_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    loss_reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    loss_type_code = table.Column<short>(type: "smallint", nullable: true),
                    site_code = table.Column<short>(type: "smallint", nullable: true),
                    dept_contact = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    loss_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    dept_claim = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    sapd = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    inspector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    case_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_losses", x => x.loss_code);
                    table.ForeignKey(
                        name: "FK_losses_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_losses_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_losses_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_losses_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_records",
                schema: "dbo",
                columns: table => new
                {
                    maintenance_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    maintenance_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    maintenance_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    odometer_reading = table.Column<int>(type: "int", nullable: false),
                    service_provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    work_order_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    invoice_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    total_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    labour_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    parts_cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    parts_used = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    scheduled_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    next_service_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    next_service_odometer = table.Column<int>(type: "int", nullable: true),
                    service_interval_km = table.Column<int>(type: "int", nullable: true),
                    service_interval_days = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    still_current = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    warranty_work = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    warranty_expiry_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    authorized_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    authorization_code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    vehicle_roadworthy = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    roadworthy_certificate_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    roadworthy_expiry_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    mechanic_notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    inspection_notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    modified_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_access_code = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_records", x => x.maintenance_id);
                    table.ForeignKey(
                        name: "FK_maintenance_records_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_maintenance_records_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_maintenance_records_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Monitor",
                schema: "dbo",
                columns: table => new
                {
                    monitor_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    Capture_dat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    User_access_code = table.Column<short>(type: "smallint", nullable: true),
                    Inquiry_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Inquiry_Desc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Driver_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Driver_persalno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Driver_Site = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monitor", x => x.monitor_code);
                    table.ForeignKey(
                        name: "FK_Monitor_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Monitor_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Monitor_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "Towing",
                schema: "dbo",
                columns: table => new
                {
                    Towing_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    Call_refer = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Tow_request_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tow_request_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tow_location_start = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vehicle_problem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keys = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Site_code = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Towing", x => x.Towing_code);
                    table.ForeignKey(
                        name: "FK_Towing_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Towing_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Towing_site_Site_code",
                        column: x => x.Site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                    table.ForeignKey(
                        name: "FK_Towing_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tracking",
                schema: "dbo",
                columns: table => new
                {
                    track_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    track_num = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    gg_previous = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    gg_follow = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    install_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    remove_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    track_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    track_type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    track_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracking", x => x.track_code);
                    table.ForeignKey(
                        name: "FK_Tracking_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Tracking_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Tracking_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_assessment",
                schema: "dbo",
                columns: table => new
                {
                    vehicle_assessment_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    spare_wheel = table.Column<bool>(type: "bit", nullable: false),
                    jack = table.Column<bool>(type: "bit", nullable: false),
                    wheel_spanner = table.Column<bool>(type: "bit", nullable: false),
                    wheel_lock_key = table.Column<bool>(type: "bit", nullable: false),
                    fuel_card = table.Column<bool>(type: "bit", nullable: false),
                    license_disc = table.Column<bool>(type: "bit", nullable: false),
                    cof_disc = table.Column<bool>(type: "bit", nullable: false),
                    fire_extinguisher = table.Column<bool>(type: "bit", nullable: true),
                    first_aid_kit = table.Column<bool>(type: "bit", nullable: true),
                    assessment_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_assessment", x => x.vehicle_assessment_code);
                    table.ForeignKey(
                        name: "FK_vehicle_assessment_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_assessment_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_assessment_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicle_Damages",
                schema: "dbo",
                columns: table => new
                {
                    damage_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    temp_vmf_code = table.Column<int>(type: "int", nullable: true),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    damage_status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    damages_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modified_by_user_access_code = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicle_Damages", x => x.damage_id);
                    table.ForeignKey(
                        name: "FK_Vehicle_Damages_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Vehicle_Damages_TS_Users_modified_by_user_access_code",
                        column: x => x.modified_by_user_access_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Vehicle_Damages_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "vehicle_history",
                schema: "dbo",
                columns: table => new
                {
                    hist_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    hist_vmf_code = table.Column<int>(type: "int", nullable: false),
                    hist_vehicle_status_code = table.Column<short>(type: "smallint", nullable: true),
                    hist_date_changed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    hist_user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    hist_fleet_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_history", x => x.hist_code);
                    table.ForeignKey(
                        name: "FK_vehicle_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_history_vehicle_master_hist_vmf_code",
                        column: x => x.hist_vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_status_history",
                schema: "dbo",
                columns: table => new
                {
                    vehicle_status_history_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    vehicle_status_code = table.Column<short>(type: "smallint", nullable: false),
                    vehicle_status_description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    status_start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status_end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_status_history", x => x.vehicle_status_history_code);
                    table.ForeignKey(
                        name: "FK_vehicle_status_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_status_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_status_history_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_type_history",
                schema: "dbo",
                columns: table => new
                {
                    vehicle_type_history_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    type_code = table.Column<short>(type: "smallint", nullable: false),
                    type_start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    type_end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_type_history", x => x.vehicle_type_history_code);
                    table.ForeignKey(
                        name: "FK_vehicle_type_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_type_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vehicle_type_history_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vip_billing",
                schema: "dbo",
                columns: table => new
                {
                    vip_billing_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    site_code = table.Column<int>(type: "int", nullable: false),
                    normal_midweek_hours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    midweek_overtime_hours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vip_billing", x => x.vip_billing_code);
                    table.ForeignKey(
                        name: "FK_vip_billing_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vip_billing_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_vip_billing_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wesbank_transaction",
                schema: "dbo",
                columns: table => new
                {
                    wesbank_transaction_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fuel_card_code = table.Column<int>(type: "int", nullable: true),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    file_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wesbank_transaction", x => x.wesbank_transaction_code);
                    table.ForeignKey(
                        name: "FK_wesbank_transaction_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_wesbank_transaction_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_wesbank_transaction_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "workshop",
                schema: "dbo",
                columns: table => new
                {
                    ww_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    receive_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    receive_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    complete_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workshop", x => x.ww_code);
                    table.ForeignKey(
                        name: "FK_workshop_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_workshop_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_workshop_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "budget_amount",
                schema: "dbo",
                columns: table => new
                {
                    budget_amount_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    budget_code = table.Column<int>(type: "int", nullable: false),
                    cost_category_code = table.Column<short>(type: "smallint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_amount", x => x.budget_amount_code);
                    table.ForeignKey(
                        name: "FK_budget_amount_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_budget_amount_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_budget_amount_budget_budget_code",
                        column: x => x.budget_code,
                        principalSchema: "dbo",
                        principalTable: "budget",
                        principalColumn: "budget_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "absa_transaction",
                schema: "dbo",
                columns: table => new
                {
                    absa_transaction_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    contract_code = table.Column<int>(type: "int", nullable: true),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    fuel_card_code = table.Column<int>(type: "int", nullable: true),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absa_transaction", x => x.absa_transaction_code);
                    table.ForeignKey(
                        name: "FK_absa_transaction_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_absa_transaction_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_absa_transaction_contract_contract_code",
                        column: x => x.contract_code,
                        principalSchema: "dbo",
                        principalTable: "contract",
                        principalColumn: "contract_code");
                    table.ForeignKey(
                        name: "FK_absa_transaction_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "contract_rebillsplit",
                schema: "dbo",
                columns: table => new
                {
                    rebillsplit_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contract_code = table.Column<int>(type: "int", nullable: false),
                    rebill_percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    site_code = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_rebillsplit", x => x.rebillsplit_code);
                    table.ForeignKey(
                        name: "FK_contract_rebillsplit_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_contract_rebillsplit_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_contract_rebillsplit_contract_contract_code",
                        column: x => x.contract_code,
                        principalSchema: "dbo",
                        principalTable: "contract",
                        principalColumn: "contract_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_rebillsplit_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip_authorities",
                schema: "dbo",
                columns: table => new
                {
                    trip_authority_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contract_code = table.Column<int>(type: "int", nullable: false),
                    approver_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    approver_rank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    approver_tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    end_odo_meter = table.Column<int>(type: "int", nullable: true),
                    expiry_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    trip_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    trip_request_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    issue_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    trip_type_code = table.Column<short>(type: "smallint", nullable: false),
                    trip_incident_type_code = table.Column<short>(type: "smallint", nullable: false),
                    user_access_code = table.Column<short>(type: "smallint", nullable: true),
                    locked_for_transfer = table.Column<bool>(type: "bit", nullable: false),
                    Trip_Is_Monthly = table.Column<bool>(type: "bit", nullable: false),
                    contract_code1 = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_authorities", x => x.trip_authority_code);
                    table.ForeignKey(
                        name: "FK_trip_authorities_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_authorities_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_trip_authorities_contract_contract_code1",
                        column: x => x.contract_code1,
                        principalSchema: "dbo",
                        principalTable: "contract",
                        principalColumn: "contract_code");
                });

            migrationBuilder.CreateTable(
                name: "VehicleKilos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    registration_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    fleet_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    start_odo = table.Column<double>(type: "float", nullable: true),
                    end_odo = table.Column<double>(type: "float", nullable: true),
                    contract_code = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleKilos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleKilos_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_VehicleKilos_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_VehicleKilos_contract_contract_code",
                        column: x => x.contract_code,
                        principalSchema: "dbo",
                        principalTable: "contract",
                        principalColumn: "contract_code");
                    table.ForeignKey(
                        name: "FK_VehicleKilos_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_contract_code",
                schema: "dbo",
                table: "absa_transaction",
                column: "contract_code");

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_created_by_user_code",
                schema: "dbo",
                table: "absa_transaction",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_modified_by_user_code",
                schema: "dbo",
                table: "absa_transaction",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_vmf_code",
                schema: "dbo",
                table: "absa_transaction",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_codes_created_by_user_code",
                schema: "dbo",
                table: "absa_transaction_codes",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_absa_transaction_codes_modified_by_user_code",
                schema: "dbo",
                table: "absa_transaction_codes",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_acc_type_created_by_user_code",
                schema: "dbo",
                table: "acc_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_acc_type_modified_by_user_code",
                schema: "dbo",
                table: "acc_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevels_created_by_user_code",
                schema: "dbo",
                table: "AccessLevels",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevels_modified_by_user_code",
                schema: "dbo",
                table: "AccessLevels",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevels_2_created_by_user_code",
                schema: "dbo",
                table: "AccessLevels_2",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLevels_2_modified_by_user_code",
                schema: "dbo",
                table: "AccessLevels_2",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_accident_created_by_user_code",
                schema: "dbo",
                table: "accident",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_accident_modified_by_user_code",
                schema: "dbo",
                table: "accident",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_accident_vmf_code",
                schema: "dbo",
                table: "accident",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_AdHocHolidays_created_by_user_code",
                schema: "dbo",
                table: "AdHocHolidays",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_AdHocHolidays_modified_by_user_code",
                schema: "dbo",
                table: "AdHocHolidays",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Alphabets_created_by_user_code",
                schema: "dbo",
                table: "Alphabets",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Alphabets_modified_by_user_code",
                schema: "dbo",
                table: "Alphabets",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Ambulance_created_by_user_code",
                schema: "dbo",
                table: "Ambulance",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Ambulance_modified_by_user_code",
                schema: "dbo",
                table: "Ambulance",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_anchor_points_created_by_user_code",
                schema: "dbo",
                table: "anchor_points",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_anchor_points_modified_by_user_code",
                schema: "dbo",
                table: "anchor_points",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_anchor_points_vmf_code",
                schema: "dbo",
                table: "anchor_points",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_anchor_types_created_by_user_code",
                schema: "dbo",
                table: "anchor_types",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_anchor_types_modified_by_user_code",
                schema: "dbo",
                table: "anchor_types",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_approvers_created_by_user_code",
                schema: "dbo",
                table: "approvers",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_approvers_modified_by_user_code",
                schema: "dbo",
                table: "approvers",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_approvers_site_code",
                schema: "dbo",
                table: "approvers",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Verification_created_by_user_code",
                schema: "dbo",
                table: "Asset_Verification",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Verification_modified_by_user_code",
                schema: "dbo",
                table: "Asset_Verification",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Verification_site_code",
                schema: "dbo",
                table: "Asset_Verification",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Verification_verified_by",
                schema: "dbo",
                table: "Asset_Verification",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "IX_Asset_Verification_vmf_code",
                schema: "dbo",
                table: "Asset_Verification",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_auction_created_by_user_code",
                schema: "dbo",
                table: "auction",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_auction_modified_by_user_code",
                schema: "dbo",
                table: "auction",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_auction_vmf_code",
                schema: "dbo",
                table: "auction",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_created_by_user_code",
                schema: "dbo",
                table: "Audit",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_modified_by_user_code",
                schema: "dbo",
                table: "Audit",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bassegment_created_by_user_code",
                schema: "dbo",
                table: "bassegment",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bassegment_modified_by_user_code",
                schema: "dbo",
                table: "bassegment",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bassegment_site_code",
                schema: "dbo",
                table: "bassegment",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_batch_created_by_user_code",
                schema: "dbo",
                table: "batch",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_batch_modified_by_user_code",
                schema: "dbo",
                table: "batch",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_batch_export_batch_code",
                schema: "dbo",
                table: "batch_export",
                column: "batch_code");

            migrationBuilder.CreateIndex(
                name: "IX_batch_export_created_by_user_code",
                schema: "dbo",
                table: "batch_export",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_batch_export_modified_by_user_code",
                schema: "dbo",
                table: "batch_export",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_created_by_user_code",
                schema: "dbo",
                table: "bill_of_material",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_material_modified_by_user_code",
                schema: "dbo",
                table: "bill_of_material",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bitmask_def_created_by_user_code",
                schema: "dbo",
                table: "bitmask_def",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bitmask_def_modified_by_user_code",
                schema: "dbo",
                table: "bitmask_def",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_block_gg_numbers_audit_created_by_user_code",
                schema: "dbo",
                table: "block_gg_numbers",
                column: "audit_created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_block_gg_numbers_audit_modified_by_user_code",
                schema: "dbo",
                table: "block_gg_numbers",
                column: "audit_modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_block_gg_numbers_Block_ID",
                schema: "dbo",
                table: "block_gg_numbers",
                column: "Block_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Booking_address_created_by_user_code",
                schema: "dbo",
                table: "Booking_address",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Booking_address_modified_by_user_code",
                schema: "dbo",
                table: "Booking_address",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_created_by_user_code",
                schema: "dbo",
                table: "bookings",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_location_code",
                schema: "dbo",
                table: "bookings",
                column: "location_code");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_modified_by_user_code",
                schema: "dbo",
                table: "bookings",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_vmf_code",
                schema: "dbo",
                table: "bookings",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_created_by_user_code",
                schema: "dbo",
                table: "budget",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_modified_by_user_code",
                schema: "dbo",
                table: "budget",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_vmf_code",
                schema: "dbo",
                table: "budget",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_amount_budget_code",
                schema: "dbo",
                table: "budget_amount",
                column: "budget_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_amount_created_by_user_code",
                schema: "dbo",
                table: "budget_amount",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_budget_amount_modified_by_user_code",
                schema: "dbo",
                table: "budget_amount",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_burn_rate_created_by_user_code",
                schema: "dbo",
                table: "burn_rate",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_burn_rate_modified_by_user_code",
                schema: "dbo",
                table: "burn_rate",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Call_centre_created_by_user_code",
                schema: "dbo",
                table: "Call_centre",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Call_centre_modified_by_user_code",
                schema: "dbo",
                table: "Call_centre",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Call_centre_vmf_code",
                schema: "dbo",
                table: "Call_centre",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_class_created_by_user_code",
                schema: "dbo",
                table: "class",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_class_modified_by_user_code",
                schema: "dbo",
                table: "class",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_clearance_created_by_user_code",
                schema: "dbo",
                table: "clearance",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_clearance_modified_by_user_code",
                schema: "dbo",
                table: "clearance",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_clearance_vmf_code",
                schema: "dbo",
                table: "clearance",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Collection_created_by_user_code",
                schema: "dbo",
                table: "Collection",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Collection_modified_by_user_code",
                schema: "dbo",
                table: "Collection",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Collection_vmf_code",
                schema: "dbo",
                table: "Collection",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_created_by_user_code",
                schema: "dbo",
                table: "company",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_modified_by_user_code",
                schema: "dbo",
                table: "company",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_connection_type_created_by_user_code",
                schema: "dbo",
                table: "connection_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_connection_type_modified_by_user_code",
                schema: "dbo",
                table: "connection_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_ActiveVehicle_Unique",
                schema: "dbo",
                table: "contract",
                columns: new[] { "vmf_code", "still_current" },
                unique: true,
                filter: "still_current = 'Y'");

            migrationBuilder.CreateIndex(
                name: "IX_contract_ContractStatuscontract_status_code",
                schema: "dbo",
                table: "contract",
                column: "ContractStatuscontract_status_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_created_by_user_code",
                schema: "dbo",
                table: "contract",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_department_code",
                schema: "dbo",
                table: "contract",
                column: "department_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_modified_by_user_code",
                schema: "dbo",
                table: "contract",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_site_code",
                schema: "dbo",
                table: "contract",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rebillsplit_contract_code",
                schema: "dbo",
                table: "contract_rebillsplit",
                column: "contract_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rebillsplit_created_by_user_code",
                schema: "dbo",
                table: "contract_rebillsplit",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rebillsplit_modified_by_user_code",
                schema: "dbo",
                table: "contract_rebillsplit",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_rebillsplit_site_code",
                schema: "dbo",
                table: "contract_rebillsplit",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_created_by_user_code",
                schema: "dbo",
                table: "contract_status",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_modified_by_user_code",
                schema: "dbo",
                table: "contract_status",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_type_created_by_user_code",
                schema: "dbo",
                table: "Contract_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_type_modified_by_user_code",
                schema: "dbo",
                table: "Contract_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Group_Mapping_created_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Group_Mapping",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Group_Mapping_modified_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Group_Mapping",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contractor_taxi_class_contractor_id",
                schema: "dbo",
                table: "Contractor_taxi_class",
                column: "contractor_id");

            migrationBuilder.CreateIndex(
                name: "IX_Contractor_taxi_class_created_by_user_code",
                schema: "dbo",
                table: "Contractor_taxi_class",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contractor_taxi_class_modified_by_user_code",
                schema: "dbo",
                table: "Contractor_taxi_class",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_created_by_user_code",
                schema: "dbo",
                table: "Contractors",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_modified_by_user_code",
                schema: "dbo",
                table: "Contractors",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_cost_category_created_by_user_code",
                schema: "dbo",
                table: "cost_category",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_cost_category_modified_by_user_code",
                schema: "dbo",
                table: "cost_category",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_cost_revenue_map_created_by_user_code",
                schema: "dbo",
                table: "cost_revenue_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_cost_revenue_map_modified_by_user_code",
                schema: "dbo",
                table: "cost_revenue_map",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_daily_import_except_created_by_user_code",
                schema: "dbo",
                table: "daily_import_except",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_daily_import_except_modified_by_user_code",
                schema: "dbo",
                table: "daily_import_except",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_daily_transactions_created_by_user_code",
                schema: "dbo",
                table: "daily_transactions",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_daily_transactions_modified_by_user_code",
                schema: "dbo",
                table: "daily_transactions",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_daily_transactions_vmf_code",
                schema: "dbo",
                table: "daily_transactions",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_db_ddl_log_created_by_user_code",
                schema: "dbo",
                table: "db_ddl_log",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_db_ddl_log_modified_by_user_code",
                schema: "dbo",
                table: "db_ddl_log",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_db_version_created_by_user_code",
                schema: "dbo",
                table: "db_version",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_db_version_modified_by_user_code",
                schema: "dbo",
                table: "db_version",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Demo_vehicles_created_by_user_code",
                schema: "dbo",
                table: "Demo_vehicles",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Demo_vehicles_modified_by_user_code",
                schema: "dbo",
                table: "Demo_vehicles",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Demo_vehicles_site_code",
                schema: "dbo",
                table: "Demo_vehicles",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_department_created_by_user_code",
                schema: "dbo",
                table: "department",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_department_DefaultSiteSite_code",
                schema: "dbo",
                table: "department",
                column: "DefaultSiteSite_code");

            migrationBuilder.CreateIndex(
                name: "IX_department_modified_by_user_code",
                schema: "dbo",
                table: "department",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_driver_licence_created_by_user_code",
                schema: "dbo",
                table: "driver_licence",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_driver_licence_modified_by_user_code",
                schema: "dbo",
                table: "driver_licence",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_driver_licence_types_created_by_user_code",
                schema: "dbo",
                table: "driver_licence_types",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_driver_licence_types_modified_by_user_code",
                schema: "dbo",
                table: "driver_licence_types",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EduCodes_created_by_user_code",
                schema: "dbo",
                table: "EduCodes",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EduCodes_modified_by_user_code",
                schema: "dbo",
                table: "EduCodes",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EnjinNumbers_created_by_user_code",
                schema: "dbo",
                table: "EnjinNumbers",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EnjinNumbers_modified_by_user_code",
                schema: "dbo",
                table: "EnjinNumbers",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EnjinNumbers_vmf_code",
                schema: "dbo",
                table: "EnjinNumbers",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_EntraId_User_Mapping_ObjectId_Unique",
                schema: "dbo",
                table: "EntraId_User_Mapping",
                column: "entra_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntraId_User_Mapping_user_access_code",
                schema: "dbo",
                table: "EntraId_User_Mapping",
                column: "user_access_code");

            migrationBuilder.CreateIndex(
                name: "IX_error_log_created_by_user_code",
                schema: "dbo",
                table: "error_log",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_error_log_modified_by_user_code",
                schema: "dbo",
                table: "error_log",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EventMap_created_by_user_code",
                schema: "dbo",
                table: "EventMap",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_EventMap_modified_by_user_code",
                schema: "dbo",
                table: "EventMap",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_extra_codes_created_by_user_code",
                schema: "dbo",
                table: "extra_codes",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_extra_codes_modified_by_user_code",
                schema: "dbo",
                table: "extra_codes",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_extras_created_by_user_code",
                schema: "dbo",
                table: "extras",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_extras_modified_by_user_code",
                schema: "dbo",
                table: "extras",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_extras_vmf_code",
                schema: "dbo",
                table: "extras",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_financial_system_created_by_user_code",
                schema: "dbo",
                table: "financial_system",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_financial_system_modified_by_user_code",
                schema: "dbo",
                table: "financial_system",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_financial_year_created_by_user_code",
                schema: "dbo",
                table: "financial_year",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_financial_year_modified_by_user_code",
                schema: "dbo",
                table: "financial_year",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fines_created_by_user_code",
                schema: "dbo",
                table: "Fines",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fines_modified_by_user_code",
                schema: "dbo",
                table: "Fines",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fines_Site_code",
                schema: "dbo",
                table: "Fines",
                column: "Site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fines_vmf_code",
                schema: "dbo",
                table: "Fines",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_created_by_user_code",
                schema: "dbo",
                table: "fleet_notes",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_modified_by_user_code",
                schema: "dbo",
                table: "fleet_notes",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_notes_vmf_code",
                schema: "dbo",
                table: "fleet_notes",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_card_created_by_user_code",
                schema: "dbo",
                table: "Fuel_card",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_card_modified_by_user_code",
                schema: "dbo",
                table: "Fuel_card",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_card_Petrecsite",
                schema: "dbo",
                table: "Fuel_card",
                column: "Petrecsite");

            migrationBuilder.CreateIndex(
                name: "IX_Fuel_card_vmf_code",
                schema: "dbo",
                table: "Fuel_card",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_FuelCard_Code_Unique",
                schema: "dbo",
                table: "Fuel_card",
                column: "Fuel_card_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fuel_recovery_configuration_created_by_user_code",
                schema: "dbo",
                table: "fuel_recovery_configuration",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_recovery_configuration_modified_by_user_code",
                schema: "dbo",
                table: "fuel_recovery_configuration",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_tariff_created_by_user_code",
                schema: "dbo",
                table: "fuel_tariff",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_tariff_modified_by_user_code",
                schema: "dbo",
                table: "fuel_tariff",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_GG_Block_audit_created_by_user_code",
                schema: "dbo",
                table: "GG_Block",
                column: "audit_created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_GG_Block_audit_modified_by_user_code",
                schema: "dbo",
                table: "GG_Block",
                column: "audit_modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Area_created_by_user_code",
                schema: "dbo",
                table: "Incident_Area",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Incident_Area_modified_by_user_code",
                schema: "dbo",
                table: "Incident_Area",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Income_Split_TempTable_created_by_user_code",
                schema: "dbo",
                table: "Income_Split_TempTable",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Income_Split_TempTable_modified_by_user_code",
                schema: "dbo",
                table: "Income_Split_TempTable",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_created_by_user_code",
                schema: "dbo",
                table: "invoice",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_modified_by_user_code",
                schema: "dbo",
                table: "invoice",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_item_created_by_user_code",
                schema: "dbo",
                table: "invoice_item",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_item_invoice_code1",
                schema: "dbo",
                table: "invoice_item",
                column: "invoice_code1");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_item_modified_by_user_code",
                schema: "dbo",
                table: "invoice_item",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_batch_code",
                schema: "dbo",
                table: "journal",
                column: "batch_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_created_by_user_code",
                schema: "dbo",
                table: "journal",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_modified_by_user_code",
                schema: "dbo",
                table: "journal",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_created_by_user_code",
                schema: "dbo",
                table: "journal_detail_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_modified_by_user_code",
                schema: "dbo",
                table: "journal_detail_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_group_created_by_user_code",
                schema: "dbo",
                table: "journal_detail_type_group",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_group_modified_by_user_code",
                schema: "dbo",
                table: "journal_detail_type_group",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Journal_WithInvalidBasCodes_created_by_user_code",
                schema: "dbo",
                table: "Journal_WithInvalidBasCodes",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Journal_WithInvalidBasCodes_modified_by_user_code",
                schema: "dbo",
                table: "Journal_WithInvalidBasCodes",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContractTerms_CreatedBy",
                schema: "dbo",
                table: "LeaseContractTerms",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContractTerms_ModifiedBy",
                schema: "dbo",
                table: "LeaseContractTerms",
                column: "ModifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContractTerms_vmf_Code",
                schema: "dbo",
                table: "LeaseContractTerms",
                column: "vmf_Code");

            migrationBuilder.CreateIndex(
                name: "IX_Legacy_User_Credentials_UserAccessCode_Unique",
                schema: "dbo",
                table: "Legacy_User_Credentials",
                column: "user_access_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_licence_fee_created_by_user_code",
                schema: "dbo",
                table: "licence_fee",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_licence_fee_modified_by_user_code",
                schema: "dbo",
                table: "licence_fee",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_license_created_by_user_code",
                schema: "dbo",
                table: "license",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_license_modified_by_user_code",
                schema: "dbo",
                table: "license",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_created_by_user_code",
                schema: "dbo",
                table: "Locations",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_modified_by_user_code",
                schema: "dbo",
                table: "Locations",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_logbook_created_by_user_code",
                schema: "dbo",
                table: "logbook",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_logbook_modified_by_user_code",
                schema: "dbo",
                table: "logbook",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_logbook_site_code",
                schema: "dbo",
                table: "logbook",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_logbook_vmf_code",
                schema: "dbo",
                table: "logbook",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Logsheets_created_by_user_code",
                schema: "dbo",
                table: "Logsheets",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Logsheets_modified_by_user_code",
                schema: "dbo",
                table: "Logsheets",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Logsheets_site_code",
                schema: "dbo",
                table: "Logsheets",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Logsheets_vmf_code",
                schema: "dbo",
                table: "Logsheets",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Loss_type_created_by_user_code",
                schema: "dbo",
                table: "Loss_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Loss_type_modified_by_user_code",
                schema: "dbo",
                table: "Loss_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_losses_created_by_user_code",
                schema: "dbo",
                table: "losses",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_losses_modified_by_user_code",
                schema: "dbo",
                table: "losses",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_losses_site_code",
                schema: "dbo",
                table: "losses",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_losses_vmf_code",
                schema: "dbo",
                table: "losses",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_maint_profile_model_created_by_user_code",
                schema: "dbo",
                table: "maint_profile_model",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maint_profile_model_model_code",
                schema: "dbo",
                table: "maint_profile_model",
                column: "model_code");

            migrationBuilder.CreateIndex(
                name: "IX_maint_profile_model_modified_by_user_code",
                schema: "dbo",
                table: "maint_profile_model",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maint_trigger_type_created_by_user_code",
                schema: "dbo",
                table: "maint_trigger_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maint_trigger_type_modified_by_user_code",
                schema: "dbo",
                table: "maint_trigger_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_created_by_user_code",
                schema: "dbo",
                table: "maintenance_records",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_modified_by_user_code",
                schema: "dbo",
                table: "maintenance_records",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_records_vmf_code",
                schema: "dbo",
                table: "maintenance_records",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_Value_History_created_by_user_code",
                schema: "dbo",
                table: "Maintenance_Value_History",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Maintenance_Value_History_modified_by_user_code",
                schema: "dbo",
                table: "Maintenance_Value_History",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceValue_created_by_user_code",
                schema: "fin",
                table: "MaintenanceValue",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceValue_modified_by_user_code",
                schema: "fin",
                table: "MaintenanceValue",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_make_created_by_user_code",
                schema: "dbo",
                table: "make",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_make_modified_by_user_code",
                schema: "dbo",
                table: "make",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_model_created_by_user_code",
                schema: "dbo",
                table: "model",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_model_make_code",
                schema: "dbo",
                table: "model",
                column: "make_code");

            migrationBuilder.CreateIndex(
                name: "IX_model_modified_by_user_code",
                schema: "dbo",
                table: "model",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Model_KilosPerFuelLitre_created_by_user_code",
                schema: "dbo",
                table: "Model_KilosPerFuelLitre",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Model_KilosPerFuelLitre_model_code",
                schema: "dbo",
                table: "Model_KilosPerFuelLitre",
                column: "model_code");

            migrationBuilder.CreateIndex(
                name: "IX_Model_KilosPerFuelLitre_modified_by_user_code",
                schema: "dbo",
                table: "Model_KilosPerFuelLitre",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monitor_created_by_user_code",
                schema: "dbo",
                table: "Monitor",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monitor_modified_by_user_code",
                schema: "dbo",
                table: "Monitor",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monitor_vmf_code",
                schema: "dbo",
                table: "Monitor",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monthly_burn_rate_created_by_user_code",
                schema: "dbo",
                table: "Monthly_burn_rate",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monthly_burn_rate_modified_by_user_code",
                schema: "dbo",
                table: "Monthly_burn_rate",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monthly_pool_vehicles_created_by_user_code",
                schema: "dbo",
                table: "Monthly_pool_vehicles",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Monthly_pool_vehicles_modified_by_user_code",
                schema: "dbo",
                table: "Monthly_pool_vehicles",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_New_Vehicles_received_created_by_user_code",
                schema: "dbo",
                table: "New_Vehicles_received",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_New_Vehicles_received_modified_by_user_code",
                schema: "dbo",
                table: "New_Vehicles_received",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Overhead_created_by_user_code",
                schema: "fin",
                table: "Overhead",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Overhead_modified_by_user_code",
                schema: "fin",
                table: "Overhead",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Overhead_TariffParameterID",
                schema: "fin",
                table: "Overhead",
                column: "TariffParameterID");

            migrationBuilder.CreateIndex(
                name: "IX_OverheadType_created_by_user_code",
                schema: "dbo",
                table: "OverheadType",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_OverheadType_modified_by_user_code",
                schema: "dbo",
                table: "OverheadType",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_multiplier_created_by_user_code",
                schema: "dbo",
                table: "overtime_multiplier",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_overtime_multiplier_modified_by_user_code",
                schema: "dbo",
                table: "overtime_multiplier",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_part_created_by_user_code",
                schema: "dbo",
                table: "part",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_part_modified_by_user_code",
                schema: "dbo",
                table: "part",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelCustomer_created_by_user_code",
                schema: "dbo",
                table: "PastelCustomer",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelCustomer_modified_by_user_code",
                schema: "dbo",
                table: "PastelCustomer",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelGL_created_by_user_code",
                schema: "dbo",
                table: "PastelGL",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelGL_modified_by_user_code",
                schema: "dbo",
                table: "PastelGL",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelStatic_created_by_user_code",
                schema: "dbo",
                table: "PastelStatic",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PastelStatic_modified_by_user_code",
                schema: "dbo",
                table: "PastelStatic",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_created_by_user_code",
                schema: "dbo",
                table: "Positions",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_modified_by_user_code",
                schema: "dbo",
                table: "Positions",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_posting_month_created_by_user_code",
                schema: "dbo",
                table: "posting_month",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_posting_month_modified_by_user_code",
                schema: "dbo",
                table: "posting_month",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_pre_vehicle_master_created_by_user_code",
                schema: "dbo",
                table: "pre_vehicle_master",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_pre_vehicle_master_modified_by_user_code",
                schema: "dbo",
                table: "pre_vehicle_master",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PreVehicle_Master_note_created_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PreVehicle_Master_note_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PreVehicle_Master_note_temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "temp_vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Private_hire_created_by_user_code",
                schema: "dbo",
                table: "Private_hire",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Private_hire_modified_by_user_code",
                schema: "dbo",
                table: "Private_hire",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_province_created_by_user_code",
                schema: "dbo",
                table: "province",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_province_modified_by_user_code",
                schema: "dbo",
                table: "province",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_province_segment_map_created_by_user_code",
                schema: "dbo",
                table: "province_segment_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_province_segment_map_modified_by_user_code",
                schema: "dbo",
                table: "province_segment_map",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_province_segment_map_segment_code",
                schema: "dbo",
                table: "province_segment_map",
                column: "segment_code");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_category_created_by_user_code",
                schema: "dbo",
                table: "purchase_category",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_category_modified_by_user_code",
                schema: "dbo",
                table: "purchase_category",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_ranks_created_by_user_code",
                schema: "dbo",
                table: "ranks",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_ranks_modified_by_user_code",
                schema: "dbo",
                table: "ranks",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_RT57_created_by_user_code",
                schema: "fin",
                table: "RT57",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_RT57_modified_by_user_code",
                schema: "fin",
                table: "RT57",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_type_created_by_user_code",
                schema: "dbo",
                table: "segment_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_type_modified_by_user_code",
                schema: "dbo",
                table: "segment_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_site_created_by_user_code",
                schema: "dbo",
                table: "site",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Site_Description_Unique",
                schema: "dbo",
                table: "site",
                column: "description",
                unique: true,
                filter: "[description] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_site_modified_by_user_code",
                schema: "dbo",
                table: "site",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_site_drivers_created_by_user_code",
                schema: "dbo",
                table: "site_drivers",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_site_drivers_modified_by_user_code",
                schema: "dbo",
                table: "site_drivers",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Status_created_by_user_code",
                schema: "Workflow",
                table: "Status",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Status_modified_by_user_code",
                schema: "Workflow",
                table: "Status",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Step_created_by_user_code",
                schema: "Workflow",
                table: "Step",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Step_modified_by_user_code",
                schema: "Workflow",
                table: "Step",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_StepType_created_by_user_code",
                schema: "Workflow",
                table: "StepType",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_StepType_modified_by_user_code",
                schema: "Workflow",
                table: "StepType",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_created_by_user_code",
                schema: "dbo",
                table: "Suppliers",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_modified_by_user_code",
                schema: "dbo",
                table: "Suppliers",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tally_created_by_user_code",
                schema: "dbo",
                table: "tally",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_tally_modified_by_user_code",
                schema: "dbo",
                table: "tally",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TariffParameter_created_by_user_code",
                schema: "fin",
                table: "TariffParameter",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TariffParameter_modified_by_user_code",
                schema: "fin",
                table: "TariffParameter",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TariffWeightCalculation_created_by_user_code",
                schema: "fin",
                table: "TariffWeightCalculation",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TariffWeightCalculation_modified_by_user_code",
                schema: "fin",
                table: "TariffWeightCalculation",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TariffWeightCalculation_TariffParameterID",
                schema: "fin",
                table: "TariffWeightCalculation",
                column: "TariffParameterID");

            migrationBuilder.CreateIndex(
                name: "IX_task_created_by_user_code",
                schema: "dbo",
                table: "task",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_task_modified_by_user_code",
                schema: "dbo",
                table: "task",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_created_by_user_code",
                schema: "dbo",
                table: "Taxis",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_department_code",
                schema: "dbo",
                table: "Taxis",
                column: "department_code");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_modified_by_user_code",
                schema: "dbo",
                table: "Taxis",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Taxis_site_code",
                schema: "dbo",
                table: "Taxis",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Temp_Vehicle_extras_created_by_user_code",
                schema: "dbo",
                table: "Temp_Vehicle_extras",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Temp_Vehicle_extras_modified_by_user_code",
                schema: "dbo",
                table: "Temp_Vehicle_extras",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_third_party_vehicle_model_description_created_by_user_code",
                schema: "dbo",
                table: "third_party_vehicle_model_description",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_third_party_vehicle_model_description_modified_by_user_code",
                schema: "dbo",
                table: "third_party_vehicle_model_description",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Tow_Truck_created_by_user_code",
                schema: "dbo",
                table: "Tow_Truck",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Tow_Truck_modified_by_user_code",
                schema: "dbo",
                table: "Tow_Truck",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Towing_created_by_user_code",
                schema: "dbo",
                table: "Towing",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Towing_modified_by_user_code",
                schema: "dbo",
                table: "Towing",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Towing_Site_code",
                schema: "dbo",
                table: "Towing",
                column: "Site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Towing_vmf_code",
                schema: "dbo",
                table: "Towing",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Tracking_created_by_user_code",
                schema: "dbo",
                table: "Tracking",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Tracking_modified_by_user_code",
                schema: "dbo",
                table: "Tracking",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Tracking_vmf_code",
                schema: "dbo",
                table: "Tracking",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Traffic_Dept_created_by_user_code",
                schema: "dbo",
                table: "Traffic_Dept",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Traffic_Dept_modified_by_user_code",
                schema: "dbo",
                table: "Traffic_Dept",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_authorities_contract_code1",
                schema: "dbo",
                table: "trip_authorities",
                column: "contract_code1");

            migrationBuilder.CreateIndex(
                name: "IX_trip_authorities_created_by_user_code",
                schema: "dbo",
                table: "trip_authorities",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_authorities_modified_by_user_code",
                schema: "dbo",
                table: "trip_authorities",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_driver_created_by_user_code",
                schema: "dbo",
                table: "trip_driver",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_driver_modified_by_user_code",
                schema: "dbo",
                table: "trip_driver",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_incident_type_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_incident_type_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_created_by_user_code",
                schema: "dbo",
                table: "trip_passengers",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_modified_by_user_code",
                schema: "dbo",
                table: "trip_passengers",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_type_created_by_user_code",
                schema: "dbo",
                table: "trip_type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_trip_type_modified_by_user_code",
                schema: "dbo",
                table: "trip_type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Log_created_by_user_code",
                schema: "dbo",
                table: "TS_Log",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Log_modified_by_user_code",
                schema: "dbo",
                table: "TS_Log",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "TS_Users",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "TS_Users",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_unit_of_measure_created_by_user_code",
                schema: "dbo",
                table: "unit_of_measure",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_unit_of_measure_modified_by_user_code",
                schema: "dbo",
                table: "unit_of_measure",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_univ_created_by_user_code",
                schema: "dbo",
                table: "univ",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_univ_modified_by_user_code",
                schema: "dbo",
                table: "univ",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_message_created_by_user_code",
                schema: "dbo",
                table: "user_message",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_message_modified_by_user_code",
                schema: "dbo",
                table: "user_message",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_message_user_access_code",
                schema: "dbo",
                table: "user_message",
                column: "user_access_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_assessment_created_by_user_code",
                schema: "dbo",
                table: "vehicle_assessment",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_assessment_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_assessment",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_assessment_vmf_code",
                schema: "dbo",
                table: "vehicle_assessment",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Damages_created_by_user_code",
                schema: "dbo",
                table: "Vehicle_Damages",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Damages_modified_by_user_access_code",
                schema: "dbo",
                table: "Vehicle_Damages",
                column: "modified_by_user_access_code");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Damages_vmf_code",
                schema: "dbo",
                table: "Vehicle_Damages",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_history_created_by_user_code",
                schema: "dbo",
                table: "vehicle_history",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_history_hist_vmf_code",
                schema: "dbo",
                table: "vehicle_history",
                column: "hist_vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_history_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_history",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_created_by_user_code",
                schema: "dbo",
                table: "vehicle_master",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_LocationId",
                schema: "dbo",
                table: "vehicle_master",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_make_code",
                schema: "dbo",
                table: "vehicle_master",
                column: "make_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_model_code1",
                schema: "dbo",
                table: "vehicle_master",
                column: "model_code1");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_master",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_master_Site_code",
                schema: "dbo",
                table: "vehicle_master",
                column: "Site_code");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_Registration_Unique",
                schema: "dbo",
                table: "vehicle_master",
                column: "registration_number",
                unique: true,
                filter: "[registration_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_VmfCode_Unique",
                schema: "dbo",
                table: "vehicle_master",
                column: "vmf_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_orders_created_by_user_code",
                schema: "dbo",
                table: "Vehicle_orders",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_orders_modified_by_user_code",
                schema: "dbo",
                table: "Vehicle_orders",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_source_created_by_user_code",
                schema: "dbo",
                table: "vehicle_source",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_source_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_source",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_status_created_by_user_code",
                schema: "dbo",
                table: "vehicle_status",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_status_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_status",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_status_history_created_by_user_code",
                schema: "dbo",
                table: "vehicle_status_history",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_status_history_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_status_history",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_status_history_vmf_code",
                schema: "dbo",
                table: "vehicle_status_history",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tariff_created_by_user_code",
                schema: "fin",
                table: "vehicle_tariff",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tariff_modified_by_user_code",
                schema: "fin",
                table: "vehicle_tariff",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tariff_TariffParameterID",
                schema: "fin",
                table: "vehicle_tariff",
                column: "TariffParameterID");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_type_history_created_by_user_code",
                schema: "dbo",
                table: "vehicle_type_history",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_type_history_modified_by_user_code",
                schema: "dbo",
                table: "vehicle_type_history",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_type_history_vmf_code",
                schema: "dbo",
                table: "vehicle_type_history",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilos_contract_code",
                schema: "dbo",
                table: "VehicleKilos",
                column: "contract_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilos_created_by_user_code",
                schema: "dbo",
                table: "VehicleKilos",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilos_modified_by_user_code",
                schema: "dbo",
                table: "VehicleKilos",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilos_vmf_code",
                schema: "dbo",
                table: "VehicleKilos",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePhotoInfo_created_by_user_code",
                schema: "dbo",
                table: "VehiclePhotoInfo",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePhotoInfo_modified_by_user_code",
                schema: "dbo",
                table: "VehiclePhotoInfo",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_version_created_by_user_code",
                schema: "dbo",
                table: "version",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_version_modified_by_user_code",
                schema: "dbo",
                table: "version",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vip_billing_created_by_user_code",
                schema: "dbo",
                table: "vip_billing",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vip_billing_modified_by_user_code",
                schema: "dbo",
                table: "vip_billing",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_vip_billing_vmf_code",
                schema: "dbo",
                table: "vip_billing",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Wesbank_KilosPerFuelLitre_created_by_user_code",
                schema: "dbo",
                table: "Wesbank_KilosPerFuelLitre",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Wesbank_KilosPerFuelLitre_modified_by_user_code",
                schema: "dbo",
                table: "Wesbank_KilosPerFuelLitre",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_wesbank_transaction_created_by_user_code",
                schema: "dbo",
                table: "wesbank_transaction",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_wesbank_transaction_modified_by_user_code",
                schema: "dbo",
                table: "wesbank_transaction",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_wesbank_transaction_vmf_code",
                schema: "dbo",
                table: "wesbank_transaction",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_created_by_user_code",
                schema: "Workflow",
                table: "Workflow",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Workflow_modified_by_user_code",
                schema: "Workflow",
                table: "Workflow",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_created_by_user_code",
                schema: "dbo",
                table: "workshop",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_modified_by_user_code",
                schema: "dbo",
                table: "workshop",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_workshop_vmf_code",
                schema: "dbo",
                table: "workshop",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_wwmerchant_created_by_user_code",
                schema: "dbo",
                table: "wwmerchant",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_wwmerchant_modified_by_user_code",
                schema: "dbo",
                table: "wwmerchant",
                column: "modified_by_user_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absa_transaction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "absa_transaction_codes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "acc_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AccessLevels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AccessLevels_2",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "accident",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AdHocHolidays",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Alphabets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Ambulance",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "anchor_points",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "anchor_types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "approvers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Asset_Verification",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "auction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Audit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "batch_export",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "bill_of_material",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "bitmask_def",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "block_gg_numbers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Booking_address",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "budget_amount",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "burn_rate",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Call_centre",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "class",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "clearance",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Collection",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "company",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "connection_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "contract_rebillsplit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Contract_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Contract_Type_Group_Mapping",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Contractor_taxi_class",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "cost_category",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "cost_revenue_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "daily_import_except",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "daily_transactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "db_ddl_log",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "db_version",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Demo_vehicles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "driver_licence",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "driver_licence_types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EduCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EnjinNumbers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EntraId_User_Mapping",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "error_log",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EventMap",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "extra_codes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "extras",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "financial_system",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "financial_year",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Fines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "fleet_notes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Fuel_card",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "fuel_recovery_configuration",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "fuel_tariff",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "fuel_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Incident_Area",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Income_Split_TempTable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "invoice_item",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "journal",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "journal_detail_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "journal_detail_type_group",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Journal_WithInvalidBasCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LeaseContractTerms",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Legacy_User_Credentials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "licence_fee",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "license",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "logbook",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Logsheets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Loss_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "losses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "maint_profile_model",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "maint_trigger_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "maintenance_records",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "maintenance_trigger",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Maintenance_Value_History",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MaintenanceValue",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "Model_KilosPerFuelLitre",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Monitor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Monthly_burn_rate",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Monthly_pool_vehicles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "New_Vehicles_received",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Overhead",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "OverheadType",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "overtime_multiplier",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "part",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PastelCustomer",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PastelGL",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PastelStatic",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "posting_month",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PreVehicle_Master_note",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Private_hire",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "province_segment_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "purchase_category",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ranks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RT57",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "segment_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "site_drivers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Status",
                schema: "Workflow");

            migrationBuilder.DropTable(
                name: "Step",
                schema: "Workflow");

            migrationBuilder.DropTable(
                name: "StepType",
                schema: "Workflow");

            migrationBuilder.DropTable(
                name: "Suppliers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "tally",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TariffWeightCalculation",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "task",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Taxis",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Temp_Vehicle_extras",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "third_party_vehicle_model_description",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tow_Truck",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Towing",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tracking",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Traffic_Dept",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "trip_authorities",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "trip_driver",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "trip_incident_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "trip_passengers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "trip_type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TS_Log",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "type",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "unit_of_measure",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "univ",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "user_message",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_assessment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Vehicle_Damages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Vehicle_orders",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_source",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_status",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_status_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_tariff",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "vehicle_type_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VehicleKilos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "VehiclePhotoInfo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "version",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vip_billing",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Wesbank_KilosPerFuelLitre",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "wesbank_transaction",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Workflow",
                schema: "Workflow");

            migrationBuilder.DropTable(
                name: "workshop",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "wwmerchant",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GG_Block",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "budget",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Contractors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "invoice",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "batch",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "pre_vehicle_master",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "bassegment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "province",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TariffParameter",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "contract",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "contract_status",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "department",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "vehicle_master",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "model",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "site",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "make",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TS_Users",
                schema: "dbo");
        }
    }
}
