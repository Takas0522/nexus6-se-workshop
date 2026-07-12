-- sqldb_fintech_03
-- Fintech payment gateway database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_fintech_03].

CREATE TABLE [merchants] (
    [merchant_id] nvarchar(450) NOT NULL,
    [merchant_name] nvarchar(max) NOT NULL,
    [merchant_category] nvarchar(max) NOT NULL,
    [settlement_currency] nvarchar(max) NOT NULL,
    [fee_rate] decimal(18,2) NOT NULL,
    [overseas_flag] bit NOT NULL,
    [contracted_at] date NOT NULL,
    CONSTRAINT [PK_merchants] PRIMARY KEY ([merchant_id])
);
GO

CREATE TABLE [transaction_alerts] (
    [alert_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [alert_type] nvarchar(max) NOT NULL,
    [alert_level] nvarchar(max) NOT NULL,
    [triggered_at] datetime2 NOT NULL,
    [notified_at] datetime2 NOT NULL,
    [resolved_at] datetime2 NULL,
    CONSTRAINT [PK_transaction_alerts] PRIMARY KEY ([alert_id])
);
GO

CREATE INDEX [IX_transaction_alerts_user_id] ON [transaction_alerts] ([user_id]);
GO
GO
