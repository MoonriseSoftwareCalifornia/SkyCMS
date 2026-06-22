using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sky.Editor.Data.Migrations.MySql
{
	/// <inheritdoc />
	public partial class AddCatalogEntryStatusCode_MySql : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
				name: "StatusCode",
				table: "ArticleCatalog",
				type: "int",
				nullable: true,
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
