-- sqldb_mobile_04
-- Mobile inventory/device database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_mobile_04].

CREATE TABLE [mobile_cost_items] (
    [cost_item_id] nvarchar(450) NOT NULL,
    [cost_type] nvarchar(max) NOT NULL,
    [currency] nvarchar(max) NOT NULL,
    [unit_cost] decimal(18,2) NOT NULL,
    [procurement_date] date NOT NULL,
    [vendor_region] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_mobile_cost_items] PRIMARY KEY ([cost_item_id])
);
GO

CREATE TABLE [mobile_device_skus] (
    [device_sku_id] nvarchar(450) NOT NULL,
    [device_name] nvarchar(max) NOT NULL,
    [supplier_region] nvarchar(max) NOT NULL,
    [import_currency] nvarchar(max) NOT NULL,
    [standard_cost] decimal(18,2) NOT NULL,
    [sales_price] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_mobile_device_skus] PRIMARY KEY ([device_sku_id])
);
GO

CREATE TABLE [network_base_stations] (
    [station_id] nvarchar(450) NOT NULL,
    [region] nvarchar(max) NOT NULL,
    [equipment_type] nvarchar(max) NOT NULL,
    [vendor_name] nvarchar(max) NOT NULL,
    [vendor_country] nvarchar(max) NOT NULL,
    [contract_currency] nvarchar(max) NOT NULL,
    [install_date] date NOT NULL,
    [maintenance_cost] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_network_base_stations] PRIMARY KEY ([station_id])
);
GO
