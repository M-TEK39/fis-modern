using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch23FinalEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenancevalue_original",
                schema: "fin",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    class_code = table.Column<short>(type: "smallint", nullable: true),
                    class_number = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    months_age = table.Column<short>(type: "smallint", nullable: false),
                    kilometer_age = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RandPerKilometer = table.Column<decimal>(
                        type: "decimal(18,2)",
                        nullable: false
                    ),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenancevalue_original", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenancevalue_original_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_maintenancevalue_original_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MaintenanceValue_Transfer",
                schema: "fin",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    class_code = table.Column<short>(type: "smallint", nullable: true),
                    class_number = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    months_age = table.Column<short>(type: "smallint", nullable: false),
                    kilometer_age = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RandPerKilometer = table.Column<decimal>(
                        type: "decimal(18,2)",
                        nullable: false
                    ),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceValue_Transfer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceValue_Transfer_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_MaintenanceValue_Transfer_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "SSIS Configurations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigurationFilter = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    ConfiguredValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackagePath = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    ConfiguredValueType = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SSIS Configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SSIS Configurations_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_SSIS Configurations_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Surcharge",
                schema: "dbo",
                columns: table => new
                {
                    surcharge_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    Site_code = table.Column<short>(type: "smallint", nullable: true),
                    RegNo1 = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    RegNo2 = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    Same = table.Column<string>(
                        type: "nvarchar(10)",
                        maxLength: 10,
                        nullable: true
                    ),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    Merchant = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    TrxDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceType = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    AuthorityNo = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surcharge", x => x.surcharge_code);
                    table.ForeignKey(
                        name: "FK_Surcharge_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Surcharge_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Surcharge_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "system_parameters",
                schema: "dbo",
                columns: table => new
                {
                    sys_parameters_code = table
                        .Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    database_version = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    app_version = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    vat_percent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    daily_weight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    hourly_weight = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_parameters", x => x.sys_parameters_code);
                    table.ForeignKey(
                        name: "FK_system_parameters_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_system_parameters_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_maintenancevalue_original_created_by_user_code",
                schema: "fin",
                table: "maintenancevalue_original",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_maintenancevalue_original_modified_by_user_code",
                schema: "fin",
                table: "maintenancevalue_original",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceValue_Transfer_created_by_user_code",
                schema: "fin",
                table: "MaintenanceValue_Transfer",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceValue_Transfer_modified_by_user_code",
                schema: "fin",
                table: "MaintenanceValue_Transfer",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_SSIS Configurations_created_by_user_code",
                schema: "dbo",
                table: "SSIS Configurations",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_SSIS Configurations_modified_by_user_code",
                schema: "dbo",
                table: "SSIS Configurations",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Surcharge_created_by_user_code",
                schema: "dbo",
                table: "Surcharge",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Surcharge_modified_by_user_code",
                schema: "dbo",
                table: "Surcharge",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Surcharge_vmf_code",
                schema: "dbo",
                table: "Surcharge",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_system_parameters_created_by_user_code",
                schema: "dbo",
                table: "system_parameters",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_system_parameters_modified_by_user_code",
                schema: "dbo",
                table: "system_parameters",
                column: "modified_by_user_code"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "maintenancevalue_original", schema: "fin");

            migrationBuilder.DropTable(name: "MaintenanceValue_Transfer", schema: "fin");

            migrationBuilder.DropTable(name: "SSIS Configurations", schema: "dbo");

            migrationBuilder.DropTable(name: "Surcharge", schema: "dbo");

            migrationBuilder.DropTable(name: "system_parameters", schema: "dbo");
        }
    }
}
