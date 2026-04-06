using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PowerTech.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncWarehouseSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tables (Reviews, SupportTickets, StockTransactions) and columns (InternalNote, UpdatedAt) 
            // already exist in the database due to manual SQL or previous unfinished migrations.
            // This migration is empty to sync the EF Core model snapshot with the actual database state.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No cleanup needed as Up was empty.
        }
    }
}
