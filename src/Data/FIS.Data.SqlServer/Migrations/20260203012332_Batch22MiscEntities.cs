using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch22MiscEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Holidays",
                schema: "dbo",
                columns: table => new
                {
                    HolidayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HolidayName = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
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
                    table.PrimaryKey("PK_Holidays", x => x.HolidayDate);
                    table.ForeignKey(
                        name: "FK_Holidays_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Holidays_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "IL",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    department_code = table.Column<double>(type: "float", nullable: true),
                    bas_installation_code = table.Column<double>(type: "float", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IL", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IL_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_IL_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "monthly_odo",
                schema: "dbo",
                columns: table => new
                {
                    monthly_odo_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    posting_month_code = table.Column<short>(type: "smallint", nullable: false),
                    max_odometer = table.Column<int>(type: "int", nullable: false),
                    derived_odo = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    odometer_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    trans_type = table.Column<string>(
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
                    table.PrimaryKey("PK_monthly_odo", x => x.monthly_odo_code);
                    table.ForeignKey(
                        name: "FK_monthly_odo_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_monthly_odo_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_monthly_odo_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Registrations",
                schema: "dbo",
                columns: table => new
                {
                    RegistrationID = table
                        .Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationNumber = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false
                    ),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.RegistrationID);
                    table.ForeignKey(
                        name: "FK_Registrations_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Registrations_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Registrations_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Report_vehicles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report_vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Report_vehicles_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Report_vehicles_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Report_vehicles_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Req_num",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    series = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    number = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Req_num", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Req_num_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Req_num_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Request_Change",
                schema: "dbo",
                columns: table => new
                {
                    request_code = table
                        .Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    request_name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    captured_by_userid = table.Column<int>(type: "int", nullable: true),
                    approved_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approved_name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    change_description = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true
                    ),
                    sub_system_affected = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    tech_description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    tech_component_impact = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true
                    ),
                    operational_impact = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true
                    ),
                    training_impact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    completion_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    approve_or_not = table.Column<string>(
                        type: "nvarchar(10)",
                        maxLength: 10,
                        nullable: true
                    ),
                    request_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Request_Change", x => x.request_code);
                    table.ForeignKey(
                        name: "FK_Request_Change_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Request_Change_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "res_person_history",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    department_code = table.Column<int>(type: "int", nullable: true),
                    department_number = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    site_code = table.Column<int>(type: "int", nullable: true),
                    res_person = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    telephone = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    net_address = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    date_changed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_res_person_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_res_person_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_res_person_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "route_details",
                schema: "dbo",
                columns: table => new
                {
                    route_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trip_authority_code = table.Column<int>(type: "int", nullable: false),
                    start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    start_odo_meter = table.Column<int>(type: "int", nullable: true),
                    end_odo_meter = table.Column<int>(type: "int", nullable: true),
                    bas_responsibility_code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    bas_object_code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    start_route_location_name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    end_route_location_name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    estimated_distance = table.Column<int>(type: "int", nullable: true),
                    bas_journal_record_code = table.Column<long>(type: "bigint", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_route_details", x => x.route_code);
                    table.ForeignKey(
                        name: "FK_route_details_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_route_details_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_created_by_user_code",
                schema: "dbo",
                table: "Holidays",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_modified_by_user_code",
                schema: "dbo",
                table: "Holidays",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_IL_created_by_user_code",
                schema: "dbo",
                table: "IL",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_IL_modified_by_user_code",
                schema: "dbo",
                table: "IL",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_monthly_odo_created_by_user_code",
                schema: "dbo",
                table: "monthly_odo",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_monthly_odo_modified_by_user_code",
                schema: "dbo",
                table: "monthly_odo",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_monthly_odo_vmf_code",
                schema: "dbo",
                table: "monthly_odo",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_created_by_user_code",
                schema: "dbo",
                table: "Registrations",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_modified_by_user_code",
                schema: "dbo",
                table: "Registrations",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_vmf_code",
                schema: "dbo",
                table: "Registrations",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Report_vehicles_created_by_user_code",
                schema: "dbo",
                table: "Report_vehicles",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Report_vehicles_modified_by_user_code",
                schema: "dbo",
                table: "Report_vehicles",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Report_vehicles_vmf_code",
                schema: "dbo",
                table: "Report_vehicles",
                column: "vmf_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Req_num_created_by_user_code",
                schema: "dbo",
                table: "Req_num",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Req_num_modified_by_user_code",
                schema: "dbo",
                table: "Req_num",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Request_Change_created_by_user_code",
                schema: "dbo",
                table: "Request_Change",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Request_Change_modified_by_user_code",
                schema: "dbo",
                table: "Request_Change",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_res_person_history_created_by_user_code",
                schema: "dbo",
                table: "res_person_history",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_res_person_history_modified_by_user_code",
                schema: "dbo",
                table: "res_person_history",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_route_details_created_by_user_code",
                schema: "dbo",
                table: "route_details",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_route_details_modified_by_user_code",
                schema: "dbo",
                table: "route_details",
                column: "modified_by_user_code"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Holidays", schema: "dbo");

            migrationBuilder.DropTable(name: "IL", schema: "dbo");

            migrationBuilder.DropTable(name: "monthly_odo", schema: "dbo");

            migrationBuilder.DropTable(name: "Registrations", schema: "dbo");

            migrationBuilder.DropTable(name: "Report_vehicles", schema: "dbo");

            migrationBuilder.DropTable(name: "Req_num", schema: "dbo");

            migrationBuilder.DropTable(name: "Request_Change", schema: "dbo");

            migrationBuilder.DropTable(name: "res_person_history", schema: "dbo");

            migrationBuilder.DropTable(name: "route_details", schema: "dbo");
        }
    }
}
