// <copyright file="20260602120010_AddCatalogEntryStatusCode_Sqlite.cs" company="Moonrise Software, LLC">
// Copyright (c) Moonrise Software, LLC. All rights reserved.
// Licensed under the MIT License (https://opensource.org/licenses/MIT)
// See https://github.com/CWALabs/SkyCMS
// for more information concerning the license and the contributors participating to this project.
// </copyright>

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sky.Editor.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddCatalogEntryStatusCode_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "ArticleCatalog",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "ArticleCatalog");
        }
    }
}
