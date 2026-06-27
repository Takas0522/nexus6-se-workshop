-- sqldb_mobile_02
-- Mobile contract management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_mobile_02].

CREATE TABLE [mnp_history] (
    [mnp_id] nvarchar(450) NOT NULL,
    [customer_id] nvarchar(450) NOT NULL,
    [mnp_type] nvarchar(max) NOT NULL,
    [from_carrier] nvarchar(max) NOT NULL,
    [to_carrier] nvarchar(max) NOT NULL,
    [executed_at] datetime2 NOT NULL,
    [trigger_reason] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_mnp_history] PRIMARY KEY ([mnp_id])
);
GO

CREATE TABLE [mobile_contracts] (
    [contract_id] nvarchar(450) NOT NULL,
    [customer_id] nvarchar(450) NOT NULL,
    [plan_id] nvarchar(max) NOT NULL,
    [device_type] nvarchar(max) NOT NULL,
    [update_month] nvarchar(max) NOT NULL,
    [subsidy_amount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_mobile_contracts] PRIMARY KEY ([contract_id])
);
GO

CREATE TABLE [mobile_customer_options] (
    [customer_option_id] nvarchar(450) NOT NULL,
    [customer_id] nvarchar(450) NOT NULL,
    [option_id] nvarchar(max) NOT NULL,
    [subscribed_at] datetime2 NOT NULL,
    [cancelled_at] datetime2 NULL,
    CONSTRAINT [PK_mobile_customer_options] PRIMARY KEY ([customer_option_id])
);
GO

CREATE TABLE [mobile_options] (
    [option_id] nvarchar(450) NOT NULL,
    [option_name] nvarchar(max) NOT NULL,
    [option_fee] decimal(18,2) NOT NULL,
    [option_category] nvarchar(max) NOT NULL,
    [valid_from] datetime2 NOT NULL,
    [valid_to] datetime2 NOT NULL,
    CONSTRAINT [PK_mobile_options] PRIMARY KEY ([option_id])
);
GO

CREATE INDEX [IX_mnp_history_customer_id] ON [mnp_history] ([customer_id]);
GO

CREATE INDEX [IX_mobile_contracts_customer_id] ON [mobile_contracts] ([customer_id]);
GO

CREATE INDEX [IX_mobile_customer_options_customer_id] ON [mobile_customer_options] ([customer_id]);
GO
