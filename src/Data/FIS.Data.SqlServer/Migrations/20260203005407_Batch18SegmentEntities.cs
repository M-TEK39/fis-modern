using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch18SegmentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_detail_allocation_exception",
                schema: "dbo",
                columns: table => new
                {
                    journal_detail_allocation_exception_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    journal_detail_allocation_exception_date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by_user_code = table.Column<int>(type: "int", nullable: false),
                    is_system_user = table.Column<bool>(type: "bit", nullable: false),
                    new_responsibility_code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_detail_allocation_exception", x => x.journal_detail_allocation_exception_code);
                    table.ForeignKey(
                        name: "FK_journal_detail_allocation_exception_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_journal_detail_allocation_exception_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "segment",
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
                    table.PrimaryKey("PK_segment", x => x.segment_code);
                    table.ForeignKey(
                        name: "FK_segment_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_site_site_code",
                        column: x => x.site_code,
                        principalSchema: "dbo",
                        principalTable: "site",
                        principalColumn: "Site_code");
                });

            migrationBuilder.CreateTable(
                name: "segment_group",
                schema: "dbo",
                columns: table => new
                {
                    segment_group_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_group_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    financial_system_code = table.Column<byte>(type: "tinyint", nullable: false),
                    segment_type_code = table.Column<byte>(type: "tinyint", nullable: true),
                    segment_group_isdebit = table.Column<bool>(type: "bit", nullable: false),
                    segment_group_isledger = table.Column<bool>(type: "bit", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segment_group", x => x.segment_group_code);
                    table.ForeignKey(
                        name: "FK_segment_group_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_group_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "segment_scoa",
                schema: "dbo",
                columns: table => new
                {
                    segment_scoa_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_group_code = table.Column<short>(type: "smallint", nullable: false),
                    segment_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    segment_part = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    scoa_version = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segment_scoa", x => x.segment_scoa_code);
                    table.ForeignKey(
                        name: "FK_segment_scoa_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_scoa_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "segment_structure_map",
                schema: "dbo",
                columns: table => new
                {
                    segment_structure_map_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_parent_code = table.Column<int>(type: "int", nullable: false),
                    segment_child_code = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segment_structure_map", x => x.segment_structure_map_code);
                    table.ForeignKey(
                        name: "FK_segment_structure_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_structure_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "segment_journal_detail_map",
                schema: "dbo",
                columns: table => new
                {
                    segment_journal_detail_map_code = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    segment_code = table.Column<int>(type: "int", nullable: false),
                    journal_detail_code = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_segment_journal_detail_map", x => x.segment_journal_detail_map_code);
                    table.ForeignKey(
                        name: "FK_segment_journal_detail_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_journal_detail_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_segment_journal_detail_map_segment_segment_code",
                        column: x => x.segment_code,
                        principalSchema: "dbo",
                        principalTable: "segment",
                        principalColumn: "segment_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_detail_type_segment_group_map",
                schema: "dbo",
                columns: table => new
                {
                    journal_detail_type_segment_group_map_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    journal_detail_type_code = table.Column<byte>(type: "tinyint", nullable: false),
                    segment_group_code = table.Column<int>(type: "int", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_detail_type_segment_group_map", x => x.journal_detail_type_segment_group_map_code);
                    table.ForeignKey(
                        name: "FK_journal_detail_type_segment_group_map_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_detail_type_segment_group_map_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_journal_detail_type_segment_group_map_journal_detail_type_journal_detail_type_code",
                        column: x => x.journal_detail_type_code,
                        principalSchema: "dbo",
                        principalTable: "journal_detail_type",
                        principalColumn: "journal_detail_type_code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_journal_detail_type_segment_group_map_segment_group_segment_group_code",
                        column: x => x.segment_group_code,
                        principalSchema: "dbo",
                        principalTable: "segment_group",
                        principalColumn: "segment_group_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_allocation_exception_created_by_user_code",
                schema: "dbo",
                table: "journal_detail_allocation_exception",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_allocation_exception_modified_by_user_code",
                schema: "dbo",
                table: "journal_detail_allocation_exception",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_segment_group_map_created_by_user_code",
                schema: "dbo",
                table: "journal_detail_type_segment_group_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_segment_group_map_journal_detail_type_code",
                schema: "dbo",
                table: "journal_detail_type_segment_group_map",
                column: "journal_detail_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_segment_group_map_modified_by_user_code",
                schema: "dbo",
                table: "journal_detail_type_segment_group_map",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_journal_detail_type_segment_group_map_segment_group_code",
                schema: "dbo",
                table: "journal_detail_type_segment_group_map",
                column: "segment_group_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_created_by_user_code",
                schema: "dbo",
                table: "segment",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_modified_by_user_code",
                schema: "dbo",
                table: "segment",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_site_code",
                schema: "dbo",
                table: "segment",
                column: "site_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_group_created_by_user_code",
                schema: "dbo",
                table: "segment_group",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_group_modified_by_user_code",
                schema: "dbo",
                table: "segment_group",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_journal_detail_map_created_by_user_code",
                schema: "dbo",
                table: "segment_journal_detail_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_journal_detail_map_modified_by_user_code",
                schema: "dbo",
                table: "segment_journal_detail_map",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_journal_detail_map_segment_code",
                schema: "dbo",
                table: "segment_journal_detail_map",
                column: "segment_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_scoa_created_by_user_code",
                schema: "dbo",
                table: "segment_scoa",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_scoa_modified_by_user_code",
                schema: "dbo",
                table: "segment_scoa",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_structure_map_created_by_user_code",
                schema: "dbo",
                table: "segment_structure_map",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_segment_structure_map_modified_by_user_code",
                schema: "dbo",
                table: "segment_structure_map",
                column: "modified_by_user_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_detail_allocation_exception",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "journal_detail_type_segment_group_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "segment_journal_detail_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "segment_scoa",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "segment_structure_map",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "segment_group",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "segment",
                schema: "dbo");
        }
    }
}
