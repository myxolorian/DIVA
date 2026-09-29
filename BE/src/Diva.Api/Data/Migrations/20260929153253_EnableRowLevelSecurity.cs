using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diva.Api.Data.Migrations
{
    /// <summary>
    /// Supabase exposes every table in the public schema through its REST API (PostgREST). Turning
    /// on row level security without any policy denies the anon/authenticated roles all access,
    /// so the API in this repo is the only reader/writer. The backend connects as the table owner
    /// (postgres), which bypasses RLS.
    /// </summary>
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <summary>Every table in the public schema, including EF's own history table.</summary>
        public static readonly string[] Tables =
        [
            "customers",
            "services",
            "orders",
            "order_items",
            "outlet_profile",
            "__EFMigrationsHistory",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
