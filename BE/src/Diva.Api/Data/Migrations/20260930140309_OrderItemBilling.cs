using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diva.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderItemBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_date",
                table: "orders",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "billed_qty",
                table: "order_items",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "line_no",
                table: "order_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "min_qty",
                table: "order_items",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "billed_qty",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "line_no",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "min_qty",
                table: "order_items");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "due_date",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }
    }
}
