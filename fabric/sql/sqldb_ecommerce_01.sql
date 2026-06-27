-- sqldb_ecommerce_01
-- EC product management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_ecommerce_01].

CREATE TABLE [categories] (
    [category_id] nvarchar(450) NOT NULL,
    [category_name] nvarchar(max) NOT NULL,
    [parent_category_id] nvarchar(max) NULL,
    [import_ratio] decimal(18,2) NOT NULL,
    [demand_elasticity] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_categories] PRIMARY KEY ([category_id])
);
GO

CREATE TABLE [price_rules] (
    [price_rule_id] nvarchar(450) NOT NULL,
    [sku] nvarchar(450) NOT NULL,
    [min_price] decimal(18,2) NOT NULL,
    [max_price] decimal(18,2) NOT NULL,
    [markdown_rule] nvarchar(max) NOT NULL,
    [valid_from] datetime2 NOT NULL,
    [valid_to] datetime2 NOT NULL,
    CONSTRAINT [PK_price_rules] PRIMARY KEY ([price_rule_id])
);
GO

CREATE TABLE [products] (
    [sku] nvarchar(450) NOT NULL,
    [product_name] nvarchar(max) NOT NULL,
    [category] nvarchar(max) NOT NULL,
    [procurement_currency] nvarchar(max) NOT NULL,
    [cost_price] decimal(18,2) NOT NULL,
    [selling_price] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_products] PRIMARY KEY ([sku])
);
GO

CREATE TABLE [sellers] (
    [seller_id] nvarchar(450) NOT NULL,
    [seller_name] nvarchar(max) NOT NULL,
    [seller_country] nvarchar(max) NOT NULL,
    [settlement_currency] nvarchar(max) NOT NULL,
    [cross_border_flag] bit NOT NULL,
    [contract_start_date] date NOT NULL,
    CONSTRAINT [PK_sellers] PRIMARY KEY ([seller_id])
);
GO

CREATE INDEX [IX_price_rules_sku] ON [price_rules] ([sku]);
GO
