using DAL.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260829030000_AddOrderIdempotency")]
public partial class AddOrderIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "idempotency_key",
            table: "orders",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_orders_idempotency_key",
            table: "orders",
            column: "idempotency_key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_orders_idempotency_key",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "idempotency_key",
            table: "orders");
    }
}
