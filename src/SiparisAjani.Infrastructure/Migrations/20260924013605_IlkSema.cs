using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiparisAjani.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IlkSema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
.Annotation("Sqlite:Autoincrement", true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
.Annotation("Sqlite:Autoincrement", true),
                    DraftId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Extractor = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LineCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AmbiguousCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NotFoundCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaskedFields = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
.Annotation("Sqlite:Autoincrement", true),
                    NormalizedProductText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderDraftLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
.Annotation("Sqlite:Autoincrement", true),
                    OrderDraftId = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNo = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MatchedSku = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    AlternativeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDraftLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderDraftLines_OrderDrafts_OrderDraftId",
                        column: x => x.OrderDraftId,
                        principalTable: "OrderDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_NormalizedName",
                table: "CatalogItems",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_Sku",
                table: "CatalogItems",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDraftLines_MatchedSku",
                table: "OrderDraftLines",
                column: "MatchedSku");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDraftLines_OrderDraftId",
                table: "OrderDraftLines",
                column: "OrderDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_CreatedAt",
                table: "OrderDrafts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDrafts_DraftId",
                table: "OrderDrafts",
                column: "DraftId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAliases_NormalizedProductText",
                table: "ProductAliases",
                column: "NormalizedProductText",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAliases_Sku",
                table: "ProductAliases",
                column: "Sku");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "OrderDraftLines");

            migrationBuilder.DropTable(
                name: "ProductAliases");

            migrationBuilder.DropTable(
                name: "OrderDrafts");
        }
    }
}