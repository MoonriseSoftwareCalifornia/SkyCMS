using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sky.Editor.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddLuceneSearchEntities_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LuceneIndexMetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantDomain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IndexName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Configuration = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentCount = table.Column<long>(type: "bigint", nullable: false),
                    IndexSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastOptimized = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    IndexVersion = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneIndexMetadata", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LuceneDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantDomain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IndexName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DocumentContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Boost = table.Column<float>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneDocuments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LuceneIndexFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TenantDomain = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IndexName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileContent = table.Column<byte[]>(type: "longblob", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneIndexFiles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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