using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMvcApp10.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchoolClassId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchoolLevelId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SchoolLevelId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolClasses_SchoolLevels_SchoolLevelId",
                        column: x => x.SchoolLevelId,
                        principalTable: "SchoolLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

                        migrationBuilder.Sql("""
                                INSERT INTO "SchoolLevels" ("Name", "SortOrder")
                                SELECT DISTINCT trim("Level"), 0
                                FROM "AspNetUsers"
                                WHERE "Level" IS NOT NULL AND trim("Level") <> '';

                                INSERT INTO "SchoolClasses" ("Name", "SchoolLevelId")
                                SELECT DISTINCT trim(u."ClassName"), l."Id"
                                FROM "AspNetUsers" AS u
                                INNER JOIN "SchoolLevels" AS l ON l."Name" = trim(u."Level")
                                WHERE u."ClassName" IS NOT NULL AND trim(u."ClassName") <> '';

                                UPDATE "AspNetUsers"
                                SET "SchoolLevelId" = (
                                        SELECT l."Id" FROM "SchoolLevels" AS l
                                        WHERE l."Name" = trim("AspNetUsers"."Level")
                                )
                                WHERE "Level" IS NOT NULL AND trim("Level") <> '';

                                UPDATE "AspNetUsers"
                                SET "SchoolClassId" = (
                                        SELECT c."Id"
                                        FROM "SchoolClasses" AS c
                                        INNER JOIN "SchoolLevels" AS l ON l."Id" = c."SchoolLevelId"
                                        WHERE l."Name" = trim("AspNetUsers"."Level")
                                            AND c."Name" = trim("AspNetUsers"."ClassName")
                                )
                                WHERE "Level" IS NOT NULL AND trim("Level") <> ''
                                    AND "ClassName" IS NOT NULL AND trim("ClassName") <> '';
                                """);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SchoolClassId",
                table: "AspNetUsers",
                column: "SchoolClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SchoolLevelId",
                table: "AspNetUsers",
                column: "SchoolLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_SchoolLevelId_Name",
                table: "SchoolClasses",
                columns: new[] { "SchoolLevelId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolLevels_Name",
                table: "SchoolLevels",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SchoolClasses_SchoolClassId",
                table: "AspNetUsers",
                column: "SchoolClassId",
                principalTable: "SchoolClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_SchoolLevels_SchoolLevelId",
                table: "AspNetUsers",
                column: "SchoolLevelId",
                principalTable: "SchoolLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SchoolClasses_SchoolClassId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_SchoolLevels_SchoolLevelId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "SchoolClasses");

            migrationBuilder.DropTable(
                name: "SchoolLevels");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SchoolClassId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SchoolLevelId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolClassId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolLevelId",
                table: "AspNetUsers");
        }
    }
}
