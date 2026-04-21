using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIS.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Batch24MiscEntitiesV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreVehicle_Master_note_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropForeignKey(
                name: "FK_PreVehicle_Master_note_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropForeignKey(
                name: "FK_PreVehicle_Master_note_pre_vehicle_master_temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_incident_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_type");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_incident_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_type");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_type");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_trip_type",
                schema: "dbo",
                table: "trip_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_trip_incident_type",
                schema: "dbo",
                table: "trip_incident_type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PreVehicle_Master_note",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropIndex(
                name: "IX_PreVehicle_Master_note_temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropColumn(
                name: "Pre_Note_code",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.DropColumn(
                name: "Pre_Note_Date",
                schema: "dbo",
                table: "PreVehicle_Master_note");

            migrationBuilder.RenameTable(
                name: "trip_type",
                schema: "dbo",
                newName: "trip_types",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "trip_incident_type",
                schema: "dbo",
                newName: "trip_incident_types",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PreVehicle_Master_note",
                schema: "dbo",
                newName: "PreVehicle_Master_notes",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_trip_type_modified_by_user_code",
                schema: "dbo",
                table: "trip_types",
                newName: "IX_trip_types_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_type_created_by_user_code",
                schema: "dbo",
                table: "trip_types",
                newName: "IX_trip_types_created_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_incident_type_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_types",
                newName: "IX_trip_incident_types_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_incident_type_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_types",
                newName: "IX_trip_incident_types_created_by_user_code");

            migrationBuilder.RenameColumn(
                name: "temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "Temp_vmf_code");

            migrationBuilder.RenameColumn(
                name: "date_created",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "Comment_Date");

            migrationBuilder.RenameColumn(
                name: "created_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "Commented_By_User_Code");

            migrationBuilder.RenameColumn(
                name: "Pre_Note",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "Comment");

            migrationBuilder.RenameIndex(
                name: "IX_PreVehicle_Master_note_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "IX_PreVehicle_Master_notes_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_PreVehicle_Master_note_created_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                newName: "IX_PreVehicle_Master_notes_Commented_By_User_Code");

            migrationBuilder.AddColumn<short>(
                name: "Comment_ID",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_trip_types",
                schema: "dbo",
                table: "trip_types",
                column: "trip_type_code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_trip_incident_types",
                schema: "dbo",
                table: "trip_incident_types",
                column: "trip_incident_type_code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PreVehicle_Master_notes",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                column: "Comment_ID");

            migrationBuilder.CreateTable(
                name: "LeaseContractTermsComment",
                schema: "dbo",
                columns: table => new
                {
                    comment_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    comment_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseContractTermsComment", x => x.comment_code);
                    table.ForeignKey(
                        name: "FK_LeaseContractTermsComment_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_LeaseContractTermsComment_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "Notify_List",
                schema: "dbo",
                columns: table => new
                {
                    Notify_list_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Notify_list_desc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Notify_email1 = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notify_List", x => x.Notify_list_code);
                    table.ForeignKey(
                        name: "FK_Notify_List_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_Notify_List_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "PrivHireFuel_card",
                schema: "dbo",
                columns: table => new
                {
                    PHFuel_card_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    phv_code = table.Column<int>(type: "int", nullable: false),
                    Counter = table.Column<short>(type: "smallint", nullable: true),
                    card_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PAN_number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivHireFuel_card", x => x.PHFuel_card_code);
                    table.ForeignKey(
                        name: "FK_PrivHireFuel_card_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_PrivHireFuel_card_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "profile_history",
                schema: "dbo",
                columns: table => new
                {
                    profile_history_code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    vmf_code = table.Column<int>(type: "int", nullable: false),
                    trans_code = table.Column<int>(type: "int", nullable: true),
                    profile_code = table.Column<short>(type: "smallint", nullable: false),
                    activity_odo = table.Column<int>(type: "int", nullable: false),
                    activity_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_history", x => x.profile_history_code);
                    table.ForeignKey(
                        name: "FK_profile_history_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_profile_history_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_profile_history_vehicle_master_vmf_code",
                        column: x => x.vmf_code,
                        principalSchema: "dbo",
                        principalTable: "vehicle_master",
                        principalColumn: "vmf_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TS_Comment",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ref_number = table.Column<int>(type: "int", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Comment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TS_Comment_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TS_Comment_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateTable(
                name: "TS_Error_Code",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Error_Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    date_created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    date_updated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_by_user_code = table.Column<int>(type: "int", nullable: true),
                    modified_by_user_code = table.Column<int>(type: "int", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Error_Code", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TS_Error_Code_TS_Users_created_by_user_code",
                        column: x => x.created_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                    table.ForeignKey(
                        name: "FK_TS_Error_Code_TS_Users_modified_by_user_code",
                        column: x => x.modified_by_user_code,
                        principalSchema: "dbo",
                        principalTable: "TS_Users",
                        principalColumn: "user_access_code");
                });

            migrationBuilder.CreateIndex(
                name: "IX_type_created_by_user_code",
                schema: "dbo",
                table: "type",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_type_modified_by_user_code",
                schema: "dbo",
                table: "type",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContractTermsComment_created_by_user_code",
                schema: "dbo",
                table: "LeaseContractTermsComment",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseContractTermsComment_modified_by_user_code",
                schema: "dbo",
                table: "LeaseContractTermsComment",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Notify_List_created_by_user_code",
                schema: "dbo",
                table: "Notify_List",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_Notify_List_modified_by_user_code",
                schema: "dbo",
                table: "Notify_List",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PrivHireFuel_card_created_by_user_code",
                schema: "dbo",
                table: "PrivHireFuel_card",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_PrivHireFuel_card_modified_by_user_code",
                schema: "dbo",
                table: "PrivHireFuel_card",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_profile_history_created_by_user_code",
                schema: "dbo",
                table: "profile_history",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_profile_history_modified_by_user_code",
                schema: "dbo",
                table: "profile_history",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_profile_history_vmf_code",
                schema: "dbo",
                table: "profile_history",
                column: "vmf_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Comment_created_by_user_code",
                schema: "dbo",
                table: "TS_Comment",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Comment_modified_by_user_code",
                schema: "dbo",
                table: "TS_Comment",
                column: "modified_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Error_Code_created_by_user_code",
                schema: "dbo",
                table: "TS_Error_Code",
                column: "created_by_user_code");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Error_Code_modified_by_user_code",
                schema: "dbo",
                table: "TS_Error_Code",
                column: "modified_by_user_code");

            migrationBuilder.AddForeignKey(
                name: "FK_PreVehicle_Master_notes_TS_Users_Commented_By_User_Code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                column: "Commented_By_User_Code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_PreVehicle_Master_notes_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_incident_types_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_types",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_incident_types_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_types",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_types_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_types",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_types_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_types",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "type",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "type",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreVehicle_Master_notes_TS_Users_Commented_By_User_Code",
                schema: "dbo",
                table: "PreVehicle_Master_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_PreVehicle_Master_notes_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_incident_types_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_types");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_incident_types_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_types");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_types_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_types");

            migrationBuilder.DropForeignKey(
                name: "FK_trip_types_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_types");

            migrationBuilder.DropForeignKey(
                name: "FK_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "type");

            migrationBuilder.DropForeignKey(
                name: "FK_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "type");

            migrationBuilder.DropTable(
                name: "LeaseContractTermsComment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Notify_List",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrivHireFuel_card",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "profile_history",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TS_Comment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TS_Error_Code",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_type_created_by_user_code",
                schema: "dbo",
                table: "type");

            migrationBuilder.DropIndex(
                name: "IX_type_modified_by_user_code",
                schema: "dbo",
                table: "type");

            migrationBuilder.DropPrimaryKey(
                name: "PK_trip_types",
                schema: "dbo",
                table: "trip_types");

            migrationBuilder.DropPrimaryKey(
                name: "PK_trip_incident_types",
                schema: "dbo",
                table: "trip_incident_types");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PreVehicle_Master_notes",
                schema: "dbo",
                table: "PreVehicle_Master_notes");

            migrationBuilder.DropColumn(
                name: "Comment_ID",
                schema: "dbo",
                table: "PreVehicle_Master_notes");

            migrationBuilder.RenameTable(
                name: "trip_types",
                schema: "dbo",
                newName: "trip_type",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "trip_incident_types",
                schema: "dbo",
                newName: "trip_incident_type",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PreVehicle_Master_notes",
                schema: "dbo",
                newName: "PreVehicle_Master_note",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_trip_types_modified_by_user_code",
                schema: "dbo",
                table: "trip_type",
                newName: "IX_trip_type_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_types_created_by_user_code",
                schema: "dbo",
                table: "trip_type",
                newName: "IX_trip_type_created_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_incident_types_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                newName: "IX_trip_incident_type_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_trip_incident_types_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                newName: "IX_trip_incident_type_created_by_user_code");

            migrationBuilder.RenameColumn(
                name: "Temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "temp_vmf_code");

            migrationBuilder.RenameColumn(
                name: "Commented_By_User_Code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "created_by_user_code");

            migrationBuilder.RenameColumn(
                name: "Comment_Date",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "date_created");

            migrationBuilder.RenameColumn(
                name: "Comment",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "Pre_Note");

            migrationBuilder.RenameIndex(
                name: "IX_PreVehicle_Master_notes_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "IX_PreVehicle_Master_note_modified_by_user_code");

            migrationBuilder.RenameIndex(
                name: "IX_PreVehicle_Master_notes_Commented_By_User_Code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                newName: "IX_PreVehicle_Master_note_created_by_user_code");

            migrationBuilder.AddColumn<int>(
                name: "Pre_Note_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateTime>(
                name: "Pre_Note_Date",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_trip_type",
                schema: "dbo",
                table: "trip_type",
                column: "trip_type_code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_trip_incident_type",
                schema: "dbo",
                table: "trip_incident_type",
                column: "trip_incident_type_code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PreVehicle_Master_note",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "Pre_Note_code");

            migrationBuilder.CreateIndex(
                name: "IX_PreVehicle_Master_note_temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "temp_vmf_code");

            migrationBuilder.AddForeignKey(
                name: "FK_PreVehicle_Master_note_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_PreVehicle_Master_note_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_PreVehicle_Master_note_pre_vehicle_master_temp_vmf_code",
                schema: "dbo",
                table: "PreVehicle_Master_note",
                column: "temp_vmf_code",
                principalSchema: "dbo",
                principalTable: "pre_vehicle_master",
                principalColumn: "temp_vmf_code",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trip_incident_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_incident_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_incident_type",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_type_TS_Users_created_by_user_code",
                schema: "dbo",
                table: "trip_type",
                column: "created_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_type_TS_Users_modified_by_user_code",
                schema: "dbo",
                table: "trip_type",
                column: "modified_by_user_code",
                principalSchema: "dbo",
                principalTable: "TS_Users",
                principalColumn: "user_access_code");
        }
    }
}
