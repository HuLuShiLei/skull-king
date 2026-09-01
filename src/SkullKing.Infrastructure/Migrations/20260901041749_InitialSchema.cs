using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkullKing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Seat = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SentAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRounds = table.Column<int>(type: "INTEGER", nullable: false),
                    TurnSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    HostPlayerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRounds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FinalScores = table.Column<string>(type: "TEXT", nullable: true),
                    WinnerSeats = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomMembers",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Seat = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMembers", x => new { x.RoomId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_RoomMembers_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameMoves",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Seat = table.Column<int>(type: "INTEGER", nullable: false),
                    Bid = table.Column<int>(type: "INTEGER", nullable: true),
                    CardId = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    TigressMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMoves", x => new { x.GameId, x.Seq });
                    table.ForeignKey(
                        name: "FK_GameMoves_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSeats",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seat = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSeats", x => new { x.GameId, x.Seat });
                    table.ForeignKey(
                        name: "FK_GameSeats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Seat = table.Column<int>(type: "INTEGER", nullable: false),
                    Bid = table.Column<int>(type: "INTEGER", nullable: false),
                    TricksWon = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Bonus = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundScores_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_RoomId_SentAt",
                table: "ChatMessages",
                columns: new[] { "RoomId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_RoomId",
                table: "Games",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSeats_PlayerId",
                table: "GameSeats",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Token",
                table: "Players",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Code",
                table: "Rooms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Status",
                table: "Rooms",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoundScores_GameId_RoundNumber_Seat",
                table: "RoundScores",
                columns: new[] { "GameId", "RoundNumber", "Seat" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "GameMoves");

            migrationBuilder.DropTable(
                name: "GameSeats");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "RoomMembers");

            migrationBuilder.DropTable(
                name: "RoundScores");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
