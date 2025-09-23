using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Domain;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration.Data.Migrations
{
    [NopMigration("2025/07/26 12:00:00", "Payments.Novalnet - Create NovalnetTransactionDetails table", MigrationProcessType.Installation)]
    public class CreateNovalnetTransactionDetails : Migration
    {
        public override void Up()
        {
            if (!Schema.Table(nameof(NovalnetTransactionDetails)).Exists())
            {
                Create.TableFor<NovalnetTransactionDetails>();
            }
        }

        public override void Down()
        {
            Delete.Table(nameof(NovalnetTransactionDetails));
        }
    }
}
