using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkullKing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomPasswordPlaintext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Rooms",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "Rooms");
        }
    }
}
