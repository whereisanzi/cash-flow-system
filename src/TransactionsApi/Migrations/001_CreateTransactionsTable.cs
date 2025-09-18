using FluentMigrator;

namespace TransactionsApi.Migrations;

[Migration(001)]
public class CreateTransactionsTable : Migration
{
    public override void Up()
    {
        Create.Table("Transactions")
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("MerchantId").AsString(255).NotNullable()
            .WithColumn("Type").AsInt32().NotNullable()
            .WithColumn("Amount").AsDecimal(18, 2).NotNullable()
            .WithColumn("DateTime").AsDateTime().NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.Index("IX_Transactions_MerchantId")
            .OnTable("Transactions")
            .OnColumn("MerchantId");

        Create.Index("IX_Transactions_DateTime")
            .OnTable("Transactions")
            .OnColumn("DateTime");
    }

    public override void Down()
    {
        Delete.Index("IX_Transactions_DateTime")
            .OnTable("Transactions");

        Delete.Index("IX_Transactions_MerchantId")
            .OnTable("Transactions");

        Delete.Table("Transactions");
    }
}