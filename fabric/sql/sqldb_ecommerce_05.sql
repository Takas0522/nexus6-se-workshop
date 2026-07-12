-- sqldb_ecommerce_05
-- EC marketing database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_ecommerce_05].

CREATE TABLE [campaign_reactions] (
    [reaction_id] nvarchar(450) NOT NULL,
    [campaign_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(450) NOT NULL,
    [reaction_type] nvarchar(max) NOT NULL,
    [reaction_time] datetime2 NOT NULL,
    CONSTRAINT [PK_campaign_reactions] PRIMARY KEY ([reaction_id])
);
GO

CREATE TABLE [member_behaviors] (
    [event_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(450) NOT NULL,
    [event_type] nvarchar(max) NOT NULL,
    [event_time] datetime2 NOT NULL,
    [device_type] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_member_behaviors] PRIMARY KEY ([event_id])
);
GO

CREATE TABLE [point_campaigns] (
    [campaign_id] nvarchar(450) NOT NULL,
    [member_id] nvarchar(450) NOT NULL,
    [point_rate] decimal(18,2) NOT NULL,
    [campaign_budget] decimal(18,2) NOT NULL,
    [campaign_start_date] date NOT NULL,
    [campaign_end_date] date NOT NULL,
    CONSTRAINT [PK_point_campaigns] PRIMARY KEY ([campaign_id])
);
GO

CREATE INDEX [IX_campaign_reactions_campaign_id] ON [campaign_reactions] ([campaign_id]);
GO

CREATE INDEX [IX_campaign_reactions_member_id] ON [campaign_reactions] ([member_id]);
GO

CREATE INDEX [IX_member_behaviors_member_id] ON [member_behaviors] ([member_id]);
GO

CREATE INDEX [IX_point_campaigns_member_id] ON [point_campaigns] ([member_id]);
GO
