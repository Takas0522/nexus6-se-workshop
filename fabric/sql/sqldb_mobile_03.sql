-- sqldb_mobile_03
-- Mobile billing database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_mobile_03].

CREATE TABLE [mobile_installments] (
    [installment_id] nvarchar(450) NOT NULL,
    [contract_id] nvarchar(450) NOT NULL,
    [device_sku_id] nvarchar(max) NOT NULL,
    [total_amount] decimal(18,2) NOT NULL,
    [monthly_payment] decimal(18,2) NOT NULL,
    [remaining_months] int NOT NULL,
    [interest_rate] decimal(18,2) NOT NULL,
    [start_date] date NOT NULL,
    CONSTRAINT [PK_mobile_installments] PRIMARY KEY ([installment_id])
);
GO

CREATE TABLE [mobile_plan_rules] (
    [plan_rule_id] nvarchar(450) NOT NULL,
    [plan_id] nvarchar(max) NOT NULL,
    [base_fee] decimal(18,2) NOT NULL,
    [overage_rule] nvarchar(max) NOT NULL,
    [device_discount_rule] nvarchar(max) NOT NULL,
    [valid_from] datetime2 NOT NULL,
    [valid_to] datetime2 NOT NULL,
    CONSTRAINT [PK_mobile_plan_rules] PRIMARY KEY ([plan_rule_id])
);
GO

CREATE TABLE [mobile_usage_billings] (
    [usage_id] nvarchar(450) NOT NULL,
    [contract_id] nvarchar(450) NOT NULL,
    [usage_date] date NOT NULL,
    [voice_usage] decimal(18,2) NOT NULL,
    [data_usage] decimal(18,2) NOT NULL,
    [monthly_charge] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_mobile_usage_billings] PRIMARY KEY ([usage_id])
);
GO

CREATE INDEX [IX_mobile_installments_contract_id] ON [mobile_installments] ([contract_id]);
GO

CREATE INDEX [IX_mobile_usage_billings_contract_id] ON [mobile_usage_billings] ([contract_id]);
GO
GO
