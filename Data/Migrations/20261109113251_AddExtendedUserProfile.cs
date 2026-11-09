using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMvcApp10.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_AspNetUsers_TargetUserId",
                table: "AppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_Teams_TargetTeamId",
                table: "AppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MagicCardPurchaseDeductions_AspNetUsers_StudentId",
                table: "MagicCardPurchaseDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_MarketCardComments_AspNetUsers_UserId",
                table: "MarketCardComments");

            migrationBuilder.DropForeignKey(
                name: "FK_MarketCardReactions_AspNetUsers_UserId",
                table: "MarketCardReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_AspNetUsers_StudentId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserNotificationReads_AspNetUsers_UserId",
                table: "UserNotificationReads");

            migrationBuilder.AddColumn<string>(
                name: "AddressNotes",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApartmentNumber",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AreaCode",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthDay",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthMonth",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Building",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfessionFather",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherOccupation",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landline",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landmark",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile2",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherOccupation",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherServices",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolOrUniversity",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetNumber",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudyStage",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_AspNetUsers_TargetUserId",
                table: "AppNotifications",
                column: "TargetUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_Teams_TargetTeamId",
                table: "AppNotifications",
                column: "TargetTeamId",
                principalTable: "Teams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MagicCardPurchaseDeductions_AspNetUsers_StudentId",
                table: "MagicCardPurchaseDeductions",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketCardComments_AspNetUsers_UserId",
                table: "MarketCardComments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MarketCardReactions_AspNetUsers_UserId",
                table: "MarketCardReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_AspNetUsers_StudentId",
                table: "TeamMembers",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotificationReads_AspNetUsers_UserId",
                table: "UserNotificationReads",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_AspNetUsers_TargetUserId",
                table: "AppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_AppNotifications_Teams_TargetTeamId",
                table: "AppNotifications");

            migrationBuilder.DropForeignKey(
                name: "FK_MagicCardPurchaseDeductions_AspNetUsers_StudentId",
                table: "MagicCardPurchaseDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_MarketCardComments_AspNetUsers_UserId",
                table: "MarketCardComments");

            migrationBuilder.DropForeignKey(
                name: "FK_MarketCardReactions_AspNetUsers_UserId",
                table: "MarketCardReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamMembers_AspNetUsers_StudentId",
                table: "TeamMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserNotificationReads_AspNetUsers_UserId",
                table: "UserNotificationReads");

            migrationBuilder.DropColumn(
                name: "AddressNotes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApartmentNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AreaCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BirthDay",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BirthMonth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Building",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConfessionFather",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FatherOccupation",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Landline",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Landmark",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Mobile2",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MotherName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MotherOccupation",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OtherServices",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolOrUniversity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreetNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StudyStage",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_AspNetUsers_TargetUserId",
                table: "AppNotifications",
                column: "TargetUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppNotifications_Teams_TargetTeamId",
                table: "AppNotifications",
                column: "TargetTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MagicCardPurchaseDeductions_AspNetUsers_StudentId",
                table: "MagicCardPurchaseDeductions",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarketCardComments_AspNetUsers_UserId",
                table: "MarketCardComments",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MarketCardReactions_AspNetUsers_UserId",
                table: "MarketCardReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamMembers_AspNetUsers_StudentId",
                table: "TeamMembers",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotificationReads_AspNetUsers_UserId",
                table: "UserNotificationReads",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
