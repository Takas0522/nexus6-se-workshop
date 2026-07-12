-- sqldb_fintech_04
-- Fintech trading database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_fintech_04].

CREATE TABLE [fx_positions] (
    [position_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [product_type] nvarchar(max) NOT NULL,
    [position_amount] decimal(18,2) NOT NULL,
    [pnl_amount] decimal(18,2) NOT NULL,
    [market_currency] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_fx_positions] PRIMARY KEY ([position_id])
);
GO

CREATE TABLE [fx_rate_snapshots] (
    [rate_snapshot_id] nvarchar(450) NOT NULL,
    [base_currency] nvarchar(max) NOT NULL,
    [quote_currency] nvarchar(max) NOT NULL,
    [mid_rate] decimal(18,2) NOT NULL,
    [bid_rate] decimal(18,2) NOT NULL,
    [ask_rate] decimal(18,2) NOT NULL,
    [captured_at] datetime2 NOT NULL,
    [source] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_fx_rate_snapshots] PRIMARY KEY ([rate_snapshot_id])
);
GO

CREATE TABLE [product_rules] (
    [product_rule_id] nvarchar(450) NOT NULL,
    [product_type] nvarchar(max) NOT NULL,
    [interest_rate] decimal(18,2) NOT NULL,
    [fee_rate] decimal(18,2) NOT NULL,
    [leverage_limit] decimal(18,2) NOT NULL,
    [valid_from] datetime2 NOT NULL,
    [valid_to] datetime2 NOT NULL,
    CONSTRAINT [PK_product_rules] PRIMARY KEY ([product_rule_id])
);
GO

CREATE INDEX [IX_fx_positions_user_id] ON [fx_positions] ([user_id]);
GO
