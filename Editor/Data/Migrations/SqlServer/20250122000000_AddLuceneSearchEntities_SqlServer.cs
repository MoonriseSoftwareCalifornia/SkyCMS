using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sky.Editor.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddLuceneSearchEntities_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LuceneIndexMetadata",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Configuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentCount = table.Column<long>(type: "bigint", nullable: false),
                    IndexSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastOptimized = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IndexVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneIndexMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LuceneDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DocumentContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Boost = table.Column<float>(type: "real", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LuceneDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LuceneIndexFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantDomain = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IndexName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
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