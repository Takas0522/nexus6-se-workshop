-- sqldb_fintech_01
-- Fintech account management database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_fintech_01].

CREATE TABLE [account_links] (
    [link_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [external_account_ref] nvarchar(max) NOT NULL,
    [link_status] nvarchar(max) NOT NULL,
    [linked_at] datetime2 NOT NULL,
    CONSTRAINT [PK_account_links] PRIMARY KEY ([link_id])
);
GO

CREATE TABLE [accounts] (
    [account_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [account_type] nvarchar(max) NOT NULL,
    [opened_at] date NOT NULL,
    [balance] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_accounts] PRIMARY KEY ([account_id])
);
GO

CREATE TABLE [kyc_records] (
    [kyc_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [kyc_status] nvarchar(max) NOT NULL,
    [id_type] nvarchar(max) NOT NULL,
    [verified_at] datetime2 NOT NULL,
    [expiry_at] datetime2 NOT NULL,
    [review_result] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_kyc_records] PRIMARY KEY ([kyc_id])
);
GO

CREATE INDEX [IX_account_links_user_id] ON [account_links] ([user_id]);
GO

CREATE INDEX [IX_accounts_user_id] ON [accounts] ([user_id]);
GO

CREATE INDEX [IX_kyc_records_user_id] ON [kyc_records] ([user_id]);
GO
