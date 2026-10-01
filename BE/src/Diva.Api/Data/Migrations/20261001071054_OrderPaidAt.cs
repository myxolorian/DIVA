using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diva.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderPaidAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            // Orders paid before this column existed: the moment of payment is unknown, so the
            // order date is the best estimate.
            migrationBuilder.Sql("UPDATE orders SET paid_at = created_at WHERE payment_status = 'Lunas';");

            migrationBuilder.CreateIndex(
                name: "ix_orders_paid_at",
                table: "orders",
                column: "paid_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_paid_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paid_at",
                table: "orders");
        }
    }
}
