using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorClaw.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChannelType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Homeserver = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelConfigs");
        }
    }
}
