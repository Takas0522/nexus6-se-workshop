-- sqldb_fintech_05
-- Fintech credit/risk database
-- Derived from DemoDataGenerator EF Core SQL Server model.
-- Apply this script to Fabric SQL Database [sqldb_fintech_05].

CREATE TABLE [credit_reviews] (
    [review_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [credit_score] decimal(18,2) NOT NULL,
    [approval_status] nvarchar(max) NOT NULL,
    [review_reason] nvarchar(max) NOT NULL,
    [reviewed_at] datetime2 NOT NULL,
    CONSTRAINT [PK_credit_reviews] PRIMARY KEY ([review_id])
);
GO

CREATE TABLE [loan_balances] (
    [loan_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [loan_type] nvarchar(max) NOT NULL,
    [principal_balance] decimal(18,2) NOT NULL,
    [interest_rate] decimal(18,2) NOT NULL,
    [monthly_payment] decimal(18,2) NOT NULL,
    [maturity_date] date NOT NULL,
    [overdue_flag] bit NOT NULL,
    CONSTRAINT [PK_loan_balances] PRIMARY KEY ([loan_id])
);
GO

CREATE TABLE [revenue_risks] (
    [revenue_id] nvarchar(450) NOT NULL,
    [business_line] nvarchar(max) NOT NULL,
    [fee_revenue] decimal(18,2) NOT NULL,
    [interest_revenue] decimal(18,2) NOT NULL,
    [fx_revenue] decimal(18,2) NOT NULL,
    [risk_loss] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_revenue_risks] PRIMARY KEY ([revenue_id])
);
GO

CREATE TABLE [risk_events] (
    [risk_event_id] nvarchar(450) NOT NULL,
    [user_id] nvarchar(450) NOT NULL,
    [risk_type] nvarchar(max) NOT NULL,
    [risk_score] decimal(18,2) NOT NULL,
    [detected_at] datetime2 NOT NULL,
    CONSTRAINT [PK_risk_events] PRIMARY KEY ([risk_event_id])
);
GO

CREATE INDEX [IX_credit_reviews_user_id] ON [credit_reviews] ([user_id]);
GO

CREATE INDEX [IX_loan_balances_user_id] ON [loan_balances] ([user_id]);
GO

CREATE INDEX [IX_risk_events_user_id] ON [risk_events] ([user_id]);
GO
