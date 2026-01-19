using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripWings.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReviewTripTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key constraints first
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewTrips_TravelPackages_TravelPackageId",
                table: "ReviewTrips");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewTrips_AspNetUsers_UserId",
                table: "ReviewTrips");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_ReviewTrips_TravelPackageId",
                table: "ReviewTrips");

            migrationBuilder.DropIndex(
                name: "IX_ReviewTrips_UserId",
                table: "ReviewTrips");

            // Drop check constraint
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReviewTrip_Rating",
                table: "ReviewTrips");

            // Drop the table
            migrationBuilder.DropTable(
                name: "ReviewTrips");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TravelPackageId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewTrips", x => x.Id);
                    table.CheckConstraint("CK_ReviewTrip_Rating", "Rating >= 1 AND Rating <= 5");
                    table.ForeignKey(
                        name: "FK_ReviewTrips_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewTrips_TravelPackages_TravelPackageId",
                        column: x => x.TravelPackageId,
                        principalTable: "TravelPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTrips_TravelPackageId",
                table: "ReviewTrips",
                column: "TravelPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewTrips_UserId",
                table: "ReviewTrips",
                column: "UserId");
        }
    }
}
