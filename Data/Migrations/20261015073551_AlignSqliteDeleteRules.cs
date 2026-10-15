using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMvcApp10.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignSqliteDeleteRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceQuestionnaireSubmissions_AspNetUsers_SubmittedById",
                table: "AttendanceQuestionnaireSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolClasses_SchoolLevels_SchoolLevelId",
                table: "SchoolClasses");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceQuestionnaireSubmissions_AspNetUsers_SubmittedById",
                table: "AttendanceQuestionnaireSubmissions",
                column: "SubmittedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolClasses_SchoolLevels_SchoolLevelId",
                table: "SchoolClasses",
                column: "SchoolLevelId",
                principalTable: "SchoolLevels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceQuestionnaireSubmissions_AspNetUsers_SubmittedById",
                table: "AttendanceQuestionnaireSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolClasses_SchoolLevels_SchoolLevelId",
                table: "SchoolClasses");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceQuestionnaireSubmissions_AspNetUsers_SubmittedById",
                table: "AttendanceQuestionnaireSubmissions",
                column: "SubmittedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolClasses_SchoolLevels_SchoolLevelId",
                table: "SchoolClasses",
                column: "SchoolLevelId",
                principalTable: "SchoolLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
