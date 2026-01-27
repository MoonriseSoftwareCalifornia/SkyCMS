using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sky.Editor.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLuceneSearchEntities_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LuceneIndexMetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantDomain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentCount = table.Column<long>(type: "INTEGER", nullable: false),
                    IndexSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastOptimized = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IndexVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneIndexMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LuceneDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantDomain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DocumentId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DocumentContent = table.Column<string>(type: "TEXT", nullable: false),
                    Boost = table.Column<float>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LuceneIndexFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TenantDomain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileContent = table.Column<byte[]>(type: "BLOB", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneIndexFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LuceneDocuments_LastModified",
                table: "LuceneDocuments",
                column: "LastModified");

            migrationBuilder.CreateIndex(
                name: "IX_LuceneDocuments_TenantDomain_IndexName",
                table: "LuceneDocuments",
                columns: new[] { "TenantDomain", "IndexName" });

            migrationBuilder.CreateIndex(
                name: "IX_LuceneDocuments_TenantDomain_IndexName_DocumentId",
                table: "LuceneDocuments",
                columns: new[] { "TenantDomain", "IndexName", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexFiles_FileSize",
                table: "LuceneIndexFiles",
                column: "FileSize");

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexFiles_LastModified",
                table: "LuceneIndexFiles",
                column: "LastModified");

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexFiles_TenantDomain_IndexName",
                table: "LuceneIndexFiles",
                columns: new[] { "TenantDomain", "IndexName" });

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexFiles_TenantDomain_IndexName_FileName",
                table: "LuceneIndexFiles",
                columns: new[] { "TenantDomain", "IndexName", "FileName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexMetadata_LastOptimized",
                table: "LuceneIndexMetadata",
                column: "LastOptimized");

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexMetadata_TenantDomain",
                table: "LuceneIndexMetadata",
                column: "TenantDomain");

            migrationBuilder.CreateIndex(
                name: "IX_LuceneIndexMetadata_TenantDomain_IndexName",
                table: "LuceneIndexMetadata",
                columns: new[] { "TenantDomain", "IndexName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LuceneDocuments");

            migrationBuilder.DropTable(
                name: "LuceneIndexFiles");

            migrationBuilder.DropTable(
                name: "LuceneIndexMetadata");
        }
    }
}