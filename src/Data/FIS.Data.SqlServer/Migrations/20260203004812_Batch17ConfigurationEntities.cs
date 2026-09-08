using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch17ConfigurationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "Configuration");

            migrationBuilder.CreateTable(
                name: "Call_Centre_Counter",
                schema: "dbo",
                columns: table => new
                {
                    Call_Centre_Counter_code = table
                        .Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Call_Center_code = table.Column<decimal>(
                        type: "decimal(18,2)",
                        nullable: false
                    ),
                    CounterCC = table.Column<short>(type: "smallint", nullable: true),
                    DataCapture_id = table.Column<short>(type: "smallint", nullable: true),
                    DataCapture_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCapture_time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Call_Centre_Counter", x => x.Call_Centre_Counter_code);
                    table.ForeignKey(
                        name: "FK_Call_Centre_Counter_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Call_Centre_Counter_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "contract_status_history",
                schema: "dbo",
                columns: table => new
                {
                    contract_status_history_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contract_code = table.Column<int>(type: "int", nullable: false),
                    contract_status_code = table.Column<short>(type: "smallint", nullable: false),
                    contract_status_description = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    status_start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status_end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_contract_status_history",
                        x => x.contract_status_history_code
                    );
                    table.ForeignKey(
                        name: "FK_contract_status_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_contract_status_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_contract_status_history_contract_contract_code",
                        column: x => x.contract_code,
                        principalSchema: "dbo",
                        principalTable: "contract",
                        principalColumn: "contract_code",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_contract_status_history_contract_status_contract_status_code",
                        column: x => x.contract_status_code,
                        principalSchema: "dbo",
                        principalTable: "contract_status",
                        principalColumn: "contract_status_code",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Contract_Type_Grouping",
                schema: "dbo",
                columns: table => new
                {
                    ctg_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ctg_description = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    Is_Active = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract_Type_Grouping", x => x.ctg_code);
                    table.ForeignKey(
                        name: "FK_Contract_Type_Grouping_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Contract_Type_Grouping_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "contract_type_mapping",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Contract_Type = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    vs_code = table.Column<byte>(type: "tinyint", nullable: false),
                    type_code = table.Column<short>(type: "smallint", nullable: false),
                    Is_Lease = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_type_mapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contract_type_mapping_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_contract_type_mapping_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "dtproperties",
                schema: "dbo",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    objectid = table.Column<int>(type: "int", nullable: true),
                    property = table.Column<string>(
                        type: "nvarchar(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    value = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    lvalue = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    version = table.Column<int>(type: "int", nullable: false),
                    uvalue = table.Column<string>(
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
                    table.PrimaryKey("PK_dtproperties", x => x.id);
                    table.ForeignKey(
                        name: "FK_dtproperties_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_dtproperties_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "FIS_Survey",
                schema: "dbo",
                columns: table => new
                {
                    satisfaction_survey_code = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    used_service = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    department_fleet = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    customer_service = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    professionalism = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    quality_of_vehicles = table.Column<string>(
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
                    table.PrimaryKey("PK_FIS_Survey", x => x.satisfaction_survey_code);
                    table.ForeignKey(
                        name: "FK_FIS_Survey_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_FIS_Survey_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Parameter",
                schema: "Configuration",
                columns: table => new
                {
                    ParameterID = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(
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
                    table.PrimaryKey("PK_Parameter", x => x.ParameterID);
                    table.ForeignKey(
                        name: "FK_Parameter_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Parameter_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UserCompany",
                schema: "Configuration",
                columns: table => new
                {
                    UserCompanyID = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    Name = table.Column<string>(
                        type: "nvarchar(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    ReportLogo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ReportLogoMimeType = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    ReportLogoFilename = table.Column<string>(
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
                    table.PrimaryKey("PK_UserCompany", x => x.UserCompanyID);
                    table.ForeignKey(
                        name: "FK_UserCompany_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_UserCompany_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Contract_Type_Map",
                schema: "dbo",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ctg_code = table.Column<int>(type: "int", nullable: false),
                    contract_type = table.Column<string>(
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
                    table.PrimaryKey("PK_Contract_Type_Map", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contract_Type_Map_Contract_Type_Grouping_ctg_code",
                        column: x => x.ctg_code,
                        principalSchema: "dbo",
                        principalTable: "Contract_Type_Grouping",
                        principalColumn: "ctg_code",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_Contract_Type_Map_Contract_type_contract_type",
                        column: x => x.contract_type,
                        principalSchema: "dbo",
                        principalTable: "Contract_type",
                        principalColumn: "contract_type"
                    );
                    table.ForeignKey(
                        name: "FK_Contract_Type_Map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_Contract_Type_Map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ParameterValue",
                schema: "Configuration",
                columns: table => new
                {
                    ParameterValueID = table
                        .Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserCompanyID = table.Column<int>(type: "int", nullable: false),
                    ParameterID = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterValue", x => x.ParameterValueID);
                    table.ForeignKey(
                        name: "FK_ParameterValue_Parameter_ParameterID",
                        column: x => x.ParameterID,
                        principalSchema: "Configuration",
                        principalTable: "Parameter",
                        principalColumn: "ParameterID",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ParameterValue_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                    table.ForeignKey(
                        name: "FK_ParameterValue_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Call_Centre_Counter_created_by_user_code",
                schema: "dbo",
                table: "Call_Centre_Counter",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Call_Centre_Counter_modified_by_user_code",
                schema: "dbo",
                table: "Call_Centre_Counter",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_history_contract_code",
                schema: "dbo",
                table: "contract_status_history",
                column: "contract_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_history_contract_status_code",
                schema: "dbo",
                table: "contract_status_history",
                column: "contract_status_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_history_created_by_user_code",
                schema: "dbo",
                table: "contract_status_history",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_status_history_modified_by_user_code",
                schema: "dbo",
                table: "contract_status_history",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Grouping_created_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Grouping",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Grouping_modified_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Grouping",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Map_contract_type",
                schema: "dbo",
                table: "Contract_Type_Map",
                column: "contract_type"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Map_created_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Map",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Map_ctg_code",
                schema: "dbo",
                table: "Contract_Type_Map",
                column: "ctg_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Type_Map_modified_by_user_code",
                schema: "dbo",
                table: "Contract_Type_Map",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_type_mapping_created_by_user_code",
                schema: "dbo",
                table: "contract_type_mapping",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_contract_type_mapping_modified_by_user_code",
                schema: "dbo",
                table: "contract_type_mapping",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_dtproperties_created_by_user_code",
                schema: "dbo",
                table: "dtproperties",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_dtproperties_modified_by_user_code",
                schema: "dbo",
                table: "dtproperties",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_FIS_Survey_created_by_user_code",
                schema: "dbo",
                table: "FIS_Survey",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_FIS_Survey_modified_by_user_code",
                schema: "dbo",
                table: "FIS_Survey",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Parameter_created_by_user_code",
                schema: "Configuration",
                table: "Parameter",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Parameter_modified_by_user_code",
                schema: "Configuration",
                table: "Parameter",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ParameterValue_created_by_user_code",
                schema: "Configuration",
                table: "ParameterValue",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ParameterValue_modified_by_user_code",
                schema: "Configuration",
                table: "ParameterValue",
                column: "modified_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ParameterValue_ParameterID",
                schema: "Configuration",
                table: "ParameterValue",
                column: "ParameterID"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserCompany_created_by_user_code",
                schema: "Configuration",
                table: "UserCompany",
                column: "created_by_user_code"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserCompany_modified_by_user_code",
                schema: "Configuration",
                table: "UserCompany",
                column: "modified_by_user_code"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Call_Centre_Counter", schema: "dbo");

            migrationBuilder.DropTable(name: "contract_status_history", schema: "dbo");

            migrationBuilder.DropTable(name: "Contract_Type_Map", schema: "dbo");

            migrationBuilder.DropTable(name: "contract_type_mapping", schema: "dbo");

            migrationBuilder.DropTable(name: "dtproperties", schema: "dbo");

            migrationBuilder.DropTable(name: "FIS_Survey", schema: "dbo");

            migrationBuilder.DropTable(name: "ParameterValue", schema: "Configuration");

            migrationBuilder.DropTable(name: "UserCompany", schema: "Configuration");

            migrationBuilder.DropTable(name: "Contract_Type_Grouping", schema: "dbo");

            migrationBuilder.DropTable(name: "Parameter", schema: "Configuration");
        }
    }
}
