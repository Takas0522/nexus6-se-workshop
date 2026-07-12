-- sqldb_ecommerce_03
-- EC inventory database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_ecommerce_03].

CREATE TABLE [inventory] (
    [inventory_id] nvarchar(450) NOT NULL,
    [sku] nvarchar(450) NOT NULL,
    [warehouse_id] nvarchar(max) NOT NULL,
    [stock_qty] int NOT NULL,
    [arrival_date] date NOT NULL,
    [import_currency] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_inventory] PRIMARY KEY ([inventory_id])
);
GO

CREATE TABLE [shipping_routes] (
    [shipping_route_id] nvarchar(450) NOT NULL,
    [warehouse_id] nvarchar(max) NOT NULL,
    [destination_region] nvarchar(max) NOT NULL,
    [lead_time_days] int NOT NULL,
    [shipping_cost] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_shipping_routes] PRIMARY KEY ([shipping_route_id])
);
GO

CREATE INDEX [IX_inventory_sku] ON [inventory] ([sku]);
GO
