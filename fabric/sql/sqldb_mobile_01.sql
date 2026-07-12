-- sqldb_mobile_01
-- Mobile customer management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_mobile_01].

CREATE TABLE [mobile_customers] (
    [customer_id] nvarchar(450) NOT NULL,
    [customer_type] nvarchar(max) NOT NULL,
    [region] nvarchar(max) NOT NULL,
    [age_band] nvarchar(max) NOT NULL,
    [segment] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_mobile_customers] PRIMARY KEY ([customer_id])
);
GO
