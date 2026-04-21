using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch21RefDataEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GG_Blocks",
                schema: "dbo",
                columns: table => new
                {
                    Block_ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Creation_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Created_By_User_Code = table.Column<int>(type: "int", nullable: true),
                    Vch_Start_Reg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Vch_End_Reg = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Modified_User_Code = table.Column<int>(type: "int", nullable: true),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GG_Blocks", x => x.Block_ID);
                    table.ForeignKey(
                        name: "FK_GG_Blocks_TS_Users_Created_By_User_Code",
                        column: x => x.Created_By_User_Code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_GG_Blocks_TS_Users_Modified_User_Code",
                        column: x => x.Modified_User_Code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "HistStatus",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: true),
                    status_code = table.Column<int>(type: "int", nullable: true),
                    status_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ggno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistStatus_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_HistStatus_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_HistStatus_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code");
                });

            migrationBuilder.CreateTable(
                name: "Merchant",
                schema: "dbo",
                columns: table => new
                {
                    Merchant_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Merchant_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merchant", x => x.Merchant_code);
                    table.ForeignKey(
                        name: "FK_Merchant_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Merchant_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "mf_code",
                schema: "dbo",
                columns: table => new
                {
                    mf_code_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    mf_code_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    mf_code_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    mf_code_value_mask = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    segment_group_code = table.Column<int>(type: "int", nullable: false),
                    mf_code_order = table.Column<byte>(type: "tinyint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mf_code", x => x.mf_code_code);
                    table.ForeignKey(
                        name: "FK_mf_code_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_mf_code_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_mf_code_segment_group_segment_group_code",
                        column: x => x.segment_group_code,
                        principalSchema: "dbo",
                        principalTable: "segment_group",
                        principalColumn: "segment_group_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "posting_year",
                schema: "dbo",
                columns: table => new
                {
                    posting_year_code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    year_start_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    year_end_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_year", x => x.posting_year_code);
                    table.ForeignKey(
                        name: "FK_posting_year_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_posting_year_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "mf_code_map",
                schema: "dbo",
                columns: table => new
                {
                    mf_code_map_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_journal_detail_map_code = table.Column<long>(type: "bigint", nullable: false),
                    mf_code_code = table.Column<int>(type: "int", nullable: false),
                    mf_code_value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mf_code_map", x => x.mf_code_map_code);
                    table.ForeignKey(
                        name: "FK_mf_code_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_mf_code_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_mf_code_map_mf_code_mf_code_code",
                        column: x => x.mf_code_code,
                        principalSchema: "dbo",
                        principalTable: "mf_code",
                        principalColumn: "mf_code_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mf_code_map_segment_journal_detail_map_segment_journal_detail_map_code",
                        column: x => x.segment_journal_detail_map_code,
                        principalSchema: "dbo",
                        principalTable: "segment_journal_detail_map",
                        principalColumn: "segment_journal_detail_map_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GG_Blocks_Created_By_User_Code",
                schema: "dbo",
                table: "GG_Blocks",
                column: "Created_By_User_Code");

            migrationBuilder.CreateIndex(
                name: "IX_GG_Blocks_Modified_User_Code",
                schema: "dbo",
                table: "GG_Blocks",
                column: "Modified_User_Code");

            migrationBuilder.CreateIndex(
                name: "IX_HistStatus_created_by_user_code",
                schema: "dbo",
                table: "HistStatus",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_HistStatus_modified_by_user_code",
                schema: "dbo",
                table: "HistStatus",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_HistStatus_vmf_code",
                schema: "dbo",
                table: "HistStatus",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_Merchant_created_by_user_code",
                schema: "dbo",
                table: "Merchant",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Merchant_modified_by_user_code",
                schema: "dbo",
                table: "Merchant",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_created_by_user_code",
                schema: "dbo",
                table: "mf_code",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_modified_by_user_code",
                schema: "dbo",
                table: "mf_code",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_segment_group_code",
                schema: "dbo",
                table: "mf_code",
                column: "segment_group_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_map_created_by_user_code",
                schema: "dbo",
                table: "mf_code_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_map_mf_code_code",
                schema: "dbo",
                table: "mf_code_map",
                column: "mf_code_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_map_modified_by_user_code",
                schema: "dbo",
                table: "mf_code_map",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_mf_code_map_segment_journal_detail_map_code",
                schema: "dbo",
                table: "mf_code_map",
                column: "segment_journal_detail_map_code");

            migrationBuilder.CreateIndex(
                name: "IX_posting_year_created_by_user_code",
                schema: "dbo",
                table: "posting_year",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_posting_year_modified_by_user_code",
                schema: "dbo",
                table: "posting_year",
                column: "modified_by_user_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GG_Blocks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "HistStatus",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Merchant",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "mf_code_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "posting_year",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "mf_code",
                schema: "dbo");
        }
    }
}
