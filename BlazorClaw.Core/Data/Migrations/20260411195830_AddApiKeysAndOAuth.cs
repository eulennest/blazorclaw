using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorClaw.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeysAndOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OAuthServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WellKnownUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ClientSecret = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TokenEndpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AuthEndpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthTokens_OAuthServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "OAuthServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    OAuthTokenId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Value = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    TokenType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_OAuthTokens_OAuthTokenId",
                        column: x => x.OAuthTokenId,
                        principalTable: "OAuthTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Identifier_UserId",
                table: "ApiKeys",
                columns: new[] { "Identifier", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OAuthTokenId",
                table: "ApiKeys",
                column: "OAuthTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokens_ServerId",
                table: "OAuthTokens",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "OAuthTokens");

            migrationBuilder.DropTable(
                name: "OAuthServers");
        }
    }
}
