using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diva.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RealPriceList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000106"));

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "services",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                // Services added by hand before this migration land in "Lainnya" (other).
                defaultValue: "Lainnya");

            migrationBuilder.AddColumn<decimal>(
                name: "max_price",
                table: "services",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_qty",
                table: "services",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "services",
                type: "integer",
                nullable: false,
                // ...and are listed after the price list.
                defaultValue: 1000);

            migrationBuilder.InsertData(
                table: "services",
                columns: new[] { "id", "category", "is_active", "max_price", "min_qty", "name", "price", "sort_order", "unit" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000201"), "Laundry Kiloan", true, null, 3m, "Cuci Kering Setrika", 7000m, 10, "Kg" },
                    { new Guid("00000000-0000-0000-0000-000000000202"), "Laundry Kiloan", true, null, 3m, "Cuci Kering Lipat", 5000m, 20, "Kg" },
                    { new Guid("00000000-0000-0000-0000-000000000203"), "Bed Cover", true, null, null, "Bed Cover Single (S)", 30000m, 30, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000204"), "Bed Cover", true, null, null, "Bed Cover Single (S) - Set", 45000m, 40, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000205"), "Bed Cover", true, null, null, "Bed Cover Double (L)", 35000m, 50, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000206"), "Bed Cover", true, null, null, "Bed Cover Double (L) - Set", 55000m, 60, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000207"), "Bed Cover", true, null, null, "Bed Cover Big Size", 50000m, 70, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000208"), "Bed Cover", true, null, null, "Bed Cover Big Size - Set", 70000m, 80, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000209"), "Selimut", true, null, null, "Selimut Tipis/Bulu Ukuran Kecil", 25000m, 90, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000210"), "Selimut", true, null, null, "Selimut Tipis/Bulu Ukuran Besar", 35000m, 100, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000211"), "Karpet / Permadani", true, null, 4m, "Karpet Tipis", 15000m, 110, "M2" },
                    { new Guid("00000000-0000-0000-0000-000000000212"), "Karpet / Permadani", true, null, 4m, "Karpet Sedang", 20000m, 120, "M2" },
                    { new Guid("00000000-0000-0000-0000-000000000213"), "Karpet / Permadani", true, null, 4m, "Karpet Tebal", 25000m, 130, "M2" },
                    { new Guid("00000000-0000-0000-0000-000000000214"), "Laundry Satuan", true, null, null, "Atasan (Kemeja/Blus/Batik/T-Shirt)", 25000m, 140, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000215"), "Laundry Satuan", true, null, null, "Bawahan (Celana/Rok/Jeans)", 25000m, 150, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000216"), "Pakaian Formal", true, null, null, "Jas/Blazer", 35000m, 160, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000217"), "Pakaian Formal", true, null, null, "Setelan Jas (Jas + Celana)", 60000m, 170, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000218"), "Dress / Gaun", true, null, null, "Dress Pendek/Gaun Standard (Polos)", 40000m, 180, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000219"), "Dress / Gaun", true, 150000m, null, "Dress Pesta/Kebaya Payet", 60000m, 190, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000220"), "Dress / Gaun", true, null, null, "Dress Baju Muslim Polos", 50000m, 200, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000221"), "Dress / Gaun", true, null, null, "Dress Baju Muslim Variasi", 75000m, 210, "Pcs" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_services_sort_order",
                table: "services",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_services_sort_order",
                table: "services");

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000201"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000202"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000203"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000204"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000205"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000206"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000207"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000208"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000209"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000210"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000211"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000212"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000213"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000214"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000215"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000216"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000217"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000218"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000219"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000220"));

            migrationBuilder.DeleteData(
                table: "services",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000221"));

            migrationBuilder.DropColumn(
                name: "category",
                table: "services");

            migrationBuilder.DropColumn(
                name: "max_price",
                table: "services");

            migrationBuilder.DropColumn(
                name: "min_qty",
                table: "services");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "services");

            migrationBuilder.InsertData(
                table: "services",
                columns: new[] { "id", "is_active", "name", "price", "unit" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), true, "Cuci Kiloan", 7000m, "Kg" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), true, "Setrika", 5000m, "Kg" },
                    { new Guid("00000000-0000-0000-0000-000000000103"), true, "Cuci + Setrika", 10000m, "Kg" },
                    { new Guid("00000000-0000-0000-0000-000000000104"), true, "Bed Cover", 35000m, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000105"), true, "Selimut", 25000m, "Pcs" },
                    { new Guid("00000000-0000-0000-0000-000000000106"), true, "Cuci Sepatu", 30000m, "Pcs" }
                });
        }
    }
}
