-- sqldb_fintech_02
-- Fintech card management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_fintech_02].

CREATE TABLE [card_transactions] (
    [transaction_id] nvarchar(450) NOT NULL,
    [card_id] nvarchar(max) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [transaction_date] datetime2 NOT NULL,
    [amount] decimal(18,2) NOT NULL,
    [overseas_flag] bit NOT NULL,
    CONSTRAINT [PK_card_transactions] PRIMARY KEY ([transaction_id])
);
GO

CREATE INDEX [IX_card_transactions_user_id] ON [card_transactions] ([user_id]);
GO
