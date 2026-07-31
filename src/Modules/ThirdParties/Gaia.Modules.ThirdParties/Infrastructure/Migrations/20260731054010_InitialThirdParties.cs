using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gaia.Modules.ThirdParties.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialThirdParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "third_parties");

            migrationBuilder.CreateTable(
                name: "import_issues",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "third_parties",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: true),
                    FirstSurname = table.Column<string>(type: "text", nullable: true),
                    SecondSurname = table.Column<string>(type: "text", nullable: true),
                    PreferredName = table.Column<string>(type: "text", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PersonalEmail = table.Column<string>(type: "text", nullable: true),
                    PrimaryPhone = table.Column<string>(type: "text", nullable: true),
                    AlternatePhone = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Observations = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsNameReview = table.Column<bool>(type: "boolean", nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_third_parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Relationship = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    AlternatePhone = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "engagements",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CorporateEmail = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_engagements_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experiences",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Organization = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experiences_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    OverallLevel = table.Column<string>(type: "text", nullable: false),
                    ReadingLevel = table.Column<string>(type: "text", nullable: true),
                    WritingLevel = table.Column<string>(type: "text", nullable: true),
                    SpeakingLevel = table.Column<string>(type: "text", nullable: true),
                    Certification = table.Column<string>(type: "text", nullable: true),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_languages_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organizational_assignments",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationalUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoleName = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SourceAreaCode = table.Column<string>(type: "text", nullable: true),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizational_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organizational_assignments_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "studies",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicLevel = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Institution = table.Column<string>(type: "text", nullable: true),
                    Graduated = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationStatus = table.Column<string>(type: "text", nullable: false),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_studies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_studies_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trainings",
                schema: "third_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Institution = table.Column<string>(type: "text", nullable: true),
                    CompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trainings_third_parties_ThirdPartyId",
                        column: x => x.ThirdPartyId,
                        principalSchema: "third_parties",
                        principalTable: "third_parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_ThirdPartyId",
                schema: "third_parties",
                table: "emergency_contacts",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_engagements_ThirdPartyId",
                schema: "third_parties",
                table: "engagements",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_experiences_ThirdPartyId",
                schema: "third_parties",
                table: "experiences",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_import_issues_BatchId",
                schema: "third_parties",
                table: "import_issues",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_languages_ThirdPartyId",
                schema: "third_parties",
                table: "languages",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_organizational_assignments_ThirdPartyId",
                schema: "third_parties",
                table: "organizational_assignments",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_studies_ThirdPartyId",
                schema: "third_parties",
                table: "studies",
                column: "ThirdPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_third_parties_DocumentType_DocumentNumber",
                schema: "third_parties",
                table: "third_parties",
#pragma warning disable CA1861 // Código generado por Entity Framework.
                columns: new[] { "DocumentType", "DocumentNumber" },
#pragma warning restore CA1861
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_third_parties_FullName",
                schema: "third_parties",
                table: "third_parties",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_trainings_ThirdPartyId",
                schema: "third_parties",
                table: "trainings",
                column: "ThirdPartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emergency_contacts",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "engagements",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "experiences",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "import_issues",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "languages",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "organizational_assignments",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "studies",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "trainings",
                schema: "third_parties");

            migrationBuilder.DropTable(
                name: "third_parties",
                schema: "third_parties");
        }
    }
}
