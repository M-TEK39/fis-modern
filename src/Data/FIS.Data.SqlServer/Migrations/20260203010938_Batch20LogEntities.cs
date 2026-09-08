using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch20LogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rpt_temp",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    reg_num = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    department = table.Column<int>(type: "int", nullable: true),
                    merchant_name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    merchant_area = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    voucher_number = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    odometer = table.Column<int>(type: "int", nullable: true),
                    litres = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    capacity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpt_temp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpt_temp_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_rpt_temp_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "scan_docs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    period_begin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    period_end = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_docs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scan_docs_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_scan_docs_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_scan_docs_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Taxi_logs",
                schema: "dbo",
                columns: table => new
                {
                    log_id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<int>(type: "int", nullable: true),
                    rek_num = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    user_start_odo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    user_end_odo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    user_start_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_start_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    user_end_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_start_odo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    driver_end_odo = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    driver_start_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_start_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    driver_end_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    userid = table.Column<short>(type: "smallint", nullable: false),
                    enter_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    invoiced_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    division = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    distance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    days = table.Column<short>(type: "smallint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxi_logs", x => x.log_id);
                    table.ForeignKey(
                        name: "FK_Taxi_logs_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Taxi_logs_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Taxi_ScanDocs",
                schema: "dbo",
                columns: table => new
                {
                    taxi_scandoc_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    period_begin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    period_end = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxi_ScanDocs", x => x.taxi_scandoc_code);
                    table.ForeignKey(
                        name: "FK_Taxi_ScanDocs_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Taxi_ScanDocs_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Taxi_ScanDocs_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Taxi_white_log",
                schema: "dbo",
                columns: table => new
                {
                    Log_id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    start_odo = table.Column<long>(type: "bigint", nullable: false),
                    end_odo = table.Column<long>(type: "bigint", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    driver = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    user_access_code = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxi_white_log", x => x.Log_id);
                    table.ForeignKey(
                        name: "FK_Taxi_white_log_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Taxi_white_log_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Taxi_white_log_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_rpt_temp_created_by_user_code",
                schema: "dbo",
                table: "rpt_temp",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_rpt_temp_modified_by_user_code",
                schema: "dbo",
                table: "rpt_temp",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_scan_docs_created_by_user_code",
                schema: "dbo",
                table: "scan_docs",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_scan_docs_modified_by_user_code",
                schema: "dbo",
                table: "scan_docs",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_scan_docs_vmf_code",
                schema: "dbo",
                table: "scan_docs",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_logs_created_by_user_code",
                schema: "dbo",
                table: "Taxi_logs",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_logs_modified_by_user_code",
                schema: "dbo",
                table: "Taxi_logs",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_ScanDocs_created_by_user_code",
                schema: "dbo",
                table: "Taxi_ScanDocs",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_ScanDocs_modified_by_user_code",
                schema: "dbo",
                table: "Taxi_ScanDocs",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_ScanDocs_vmf_code",
                schema: "dbo",
                table: "Taxi_ScanDocs",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_white_log_created_by_user_code",
                schema: "dbo",
                table: "Taxi_white_log",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_white_log_modified_by_user_code",
                schema: "dbo",
                table: "Taxi_white_log",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Taxi_white_log_vmf_code",
                schema: "dbo",
                table: "Taxi_white_log",
                column: "vmf_code"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "rpt_temp", schema: "dbo");

            migrationBuilder.DropTable(name: "scan_docs", schema: "dbo");

            migrationBuilder.DropTable(name: "Taxi_logs", schema: "dbo");

            migrationBuilder.DropTable(name: "Taxi_ScanDocs", schema: "dbo");

            migrationBuilder.DropTable(name: "Taxi_white_log", schema: "dbo");
        }
    }
}
