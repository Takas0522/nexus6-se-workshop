-- sqldb_mobile_05
-- Mobile CRM database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_mobile_05].

CREATE TABLE [mobile_campaign_actions] (
    [action_id] nvarchar(450) NOT NULL,
    [customer_id] nvarchar(450) NOT NULL,
    [action_type] nvarchar(max) NOT NULL,
    [sent_at] datetime2 NOT NULL,
    [response_type] nvarchar(max) NOT NULL,
    [response_at] datetime2 NOT NULL,
    CONSTRAINT [PK_mobile_campaign_actions] PRIMARY KEY ([action_id])
);
GO

CREATE TABLE [mobile_tickets] (
    [ticket_id] nvarchar(450) NOT NULL,
    [customer_id] nvarchar(450) NOT NULL,
    [contact_reason] nvarchar(max) NOT NULL,
    [created_at] datetime2 NOT NULL,
    [resolved_at] datetime2 NOT NULL,
    [cancel_flag] bit NOT NULL,
    CONSTRAINT [PK_mobile_tickets] PRIMARY KEY ([ticket_id])
);
GO

CREATE INDEX [IX_mobile_campaign_actions_customer_id] ON [mobile_campaign_actions] ([customer_id]);
GO

CREATE INDEX [IX_mobile_tickets_customer_id] ON [mobile_tickets] ([customer_id]);
GO
