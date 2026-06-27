-- sqldb_ecommerce_02
-- EC order management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_ecommerce_02].

CREATE TABLE [orders] (
    [order_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(450) NOT NULL,
    [sku] nvarchar(450) NOT NULL,
    [order_date] date NOT NULL,
    [quantity] int NOT NULL,
    [order_amount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_orders] PRIMARY KEY ([order_id])
);
GO

CREATE TABLE [return_cancellations] (
    [return_id] nvarchar(450) NOT NULL,
    [order_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(max) NOT NULL,
    [sku] nvarchar(max) NOT NULL,
    [return_reason] nvarchar(max) NOT NULL,
    [return_amount] decimal(18,2) NOT NULL,
    [returned_at] datetime2 NOT NULL,
    CONSTRAINT [PK_return_cancellations] PRIMARY KEY ([return_id])
);
GO

CREATE INDEX [IX_orders_member_id] ON [orders] ([member_id]);
GO

CREATE INDEX [IX_orders_sku] ON [orders] ([sku]);
GO

CREATE INDEX [IX_return_cancellations_order_id] ON [return_cancellations] ([order_id]);
GO
GO
