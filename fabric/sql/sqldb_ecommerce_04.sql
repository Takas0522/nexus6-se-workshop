-- sqldb_ecommerce_04
-- EC member/point database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_ecommerce_04].

CREATE TABLE [members] (
    [member_id] nvarchar(450) NOT NULL,
    [member_name] nvarchar(max) NOT NULL,
    [email] nvarchar(max) NOT NULL,
    [rank] nvarchar(max) NOT NULL,
    [registered_at] date NOT NULL,
    [region] nvarchar(max) NOT NULL,
    [age_band] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_members] PRIMARY KEY ([member_id])
);
GO

CREATE TABLE [point_events] (
    [point_event_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(450) NOT NULL,
    [event_type] nvarchar(max) NOT NULL,
    [point_amount] int NOT NULL,
    [balance_after] int NOT NULL,
    [related_order_id] nvarchar(max) NOT NULL,
    [event_at] datetime2 NOT NULL,
    [expiry_at] datetime2 NOT NULL,
    CONSTRAINT [PK_point_events] PRIMARY KEY ([point_event_id])
);
GO

CREATE INDEX [IX_point_events_member_id] ON [point_events] ([member_id]);
GO
