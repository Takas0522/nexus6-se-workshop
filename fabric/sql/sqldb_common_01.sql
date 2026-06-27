-- sqldb_common_01
-- Common customer identity database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_common_01].

CREATE TABLE [customer_integration_events] (
    [event_id] nvarchar(450) NOT NULL,
    [unified_customer_id] nvarchar(max) NOT NULL,
    [event_type] nvarchar(max) NOT NULL,
    [domain] nvarchar(max) NOT NULL,
    [event_detail] nvarchar(max) NOT NULL,
    [occurred_at] datetime2 NOT NULL,
    CONSTRAINT [PK_customer_integration_events] PRIMARY KEY ([event_id])
);
GO

CREATE TABLE [customer_segment_assignments] (
    [assignment_id] nvarchar(450) NOT NULL,
    [unified_customer_id] nvarchar(max) NOT NULL,
    [segment_id] nvarchar(max) NOT NULL,
    [assigned_at] datetime2 NOT NULL,
    [expires_at] datetime2 NULL,
    CONSTRAINT [PK_customer_segment_assignments] PRIMARY KEY ([assignment_id])
);
GO

CREATE TABLE [customer_segment_masters] (
    [segment_id] nvarchar(450) NOT NULL,
    [segment_name] nvarchar(max) NOT NULL,
    [definition_rule] nvarchar(max) NOT NULL,
    [target_domains] nvarchar(max) NOT NULL,
    [valid_from] datetime2 NOT NULL,
    [valid_to] datetime2 NOT NULL,
    CONSTRAINT [PK_customer_segment_masters] PRIMARY KEY ([segment_id])
);
GO

CREATE TABLE [domain_id_mappings] (
    [map_id] nvarchar(450) NOT NULL,
    [unified_customer_id] nvarchar(max) NOT NULL,
    [domain] nvarchar(450) NOT NULL,
    [domain_customer_id] nvarchar(450) NOT NULL,
    [source_system] nvarchar(max) NOT NULL,
    [linked_at] datetime2 NOT NULL,
    [link_status] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_domain_id_mappings] PRIMARY KEY ([map_id])
);
GO

CREATE TABLE [unified_customers] (
    [unified_customer_id] nvarchar(450) NOT NULL,
    [full_name] nvarchar(max) NOT NULL,
    [birth_date] date NOT NULL,
    [gender] nvarchar(max) NOT NULL,
    [region] nvarchar(max) NOT NULL,
    [age_band] nvarchar(max) NOT NULL,
    [primary_email] nvarchar(max) NOT NULL,
    [primary_phone] nvarchar(max) NOT NULL,
    [kyc_status] nvarchar(max) NOT NULL,
    [registered_at] datetime2 NOT NULL,
    [last_updated_at] datetime2 NOT NULL,
    CONSTRAINT [PK_unified_customers] PRIMARY KEY ([unified_customer_id])
);
GO

CREATE INDEX [IX_domain_id_mappings_domain_domain_customer_id] ON [domain_id_mappings] ([domain], [domain_customer_id]);
GO
GO
