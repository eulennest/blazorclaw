using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorClaw.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelRegisterToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelRegisterToken",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChannelRegisterTokenExpiredAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelRegisterToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ChannelRegisterTokenExpiredAt",
                table: "AspNetUsers");
        }
    }
}
