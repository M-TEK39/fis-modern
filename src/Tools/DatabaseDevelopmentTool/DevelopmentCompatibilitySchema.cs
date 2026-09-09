namespace FIS.Tools.DatabaseDevelopmentTool;

internal static class DevelopmentCompatibilitySchema
{
    // This is a local-only additive bootstrap for the legacy objects that are
    // required to exercise the compatibility paths. It intentionally does not
    // alter an existing column or drop anything. The client schema remains the
    // source of truth for production and is handled by runtime negotiation.
    public const string Sql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF OBJECT_ID(N'dbo.site', N'U') IS NULL
            THROW 51010, 'The EF bootstrap did not create dbo.site.', 1;

        IF COL_LENGTH(N'dbo.site', N'financial_system_activate_date') IS NULL
            ALTER TABLE dbo.site ADD financial_system_activate_date smalldatetime NULL;
        IF COL_LENGTH(N'dbo.site', N'export_is_active') IS NULL
            ALTER TABLE dbo.site ADD export_is_active bit NULL;
        IF COL_LENGTH(N'dbo.site', N'date_last_exported') IS NULL
            ALTER TABLE dbo.site ADD date_last_exported smalldatetime NULL;
        IF COL_LENGTH(N'dbo.site', N'Service_Kilometres') IS NULL
            ALTER TABLE dbo.site ADD Service_Kilometres int NULL;
        IF COL_LENGTH(N'dbo.site', N'Service_Years') IS NULL
            ALTER TABLE dbo.site ADD Service_Years tinyint NULL;
        IF COL_LENGTH(N'dbo.site', N'Overhead_Percentage') IS NULL
            ALTER TABLE dbo.site ADD Overhead_Percentage decimal(6, 3) NULL;
        IF COL_LENGTH(N'dbo.site', N'province_code') IS NULL
            ALTER TABLE dbo.site ADD province_code tinyint NULL;
        IF COL_LENGTH(N'dbo.site', N'notes') IS NULL
            ALTER TABLE dbo.site ADD notes varchar(255) NULL;
        IF COL_LENGTH(N'dbo.site', N'user_access_code') IS NULL
            ALTER TABLE dbo.site ADD user_access_code smallint NULL;

        IF OBJECT_ID(N'dbo.Logsheets', N'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N'dbo.Logsheets', N'trans_date') IS NULL
                ALTER TABLE dbo.Logsheets ADD trans_date smalldatetime NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'driver_time') IS NULL
                ALTER TABLE dbo.Logsheets ADD driver_time float NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'FBS_comp') IS NULL
                ALTER TABLE dbo.Logsheets ADD FBS_comp datetime NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'user_access_code') IS NULL
                ALTER TABLE dbo.Logsheets ADD user_access_code smallint NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'trans_time') IS NULL
                ALTER TABLE dbo.Logsheets ADD trans_time time(3) NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'department_code') IS NULL
                ALTER TABLE dbo.Logsheets ADD department_code smallint NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'contract_code') IS NULL
                ALTER TABLE dbo.Logsheets ADD contract_code int NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'journal_detail_code') IS NULL
                ALTER TABLE dbo.Logsheets ADD journal_detail_code uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.Logsheets', N'parent_log_code') IS NULL
                ALTER TABLE dbo.Logsheets ADD parent_log_code int NULL;
        END;

        IF OBJECT_ID(N'dbo.Jobcards', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Jobcards
            (
                jc_code int IDENTITY(1, 1) NOT NULL,
                jc_counter int NULL,
                [year] nchar(10) NULL,
                jc_number varchar(15) NOT NULL,
                vmf_code int NOT NULL,
                extra_code int NULL,
                jcs_comment varchar(150) NULL,
                jcs_date varchar(10) NULL,
                hhandover_name varchar(50) NULL,
                hhandover_date varchar(10) NULL,
                status_code tinyint NULL,
                captured_by int NOT NULL,
                priority char(1) NULL,
                Authorizer int NULL,
                authorizer_jobcard_comments varchar(150) NULL,
                authorizer_update_date smalldatetime NULL,
                reviewed_by_Authorizer char(1) NULL,
                DateClosed smalldatetime NULL,
                CONSTRAINT PK_Jobcards PRIMARY KEY CLUSTERED (jc_code)
            );
        END;

        IF OBJECT_ID(N'dbo.Notices', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Notices
            (
                notice_id int IDENTITY(1, 1) NOT NULL,
                notice_date datetime2 NULL,
                notice_from varchar(200) NULL,
                notice_title varchar(500) NULL,
                notice_body varchar(max) NULL,
                notice_person varchar(200) NULL,
                notice_person_title varchar(200) NULL,
                date_created datetime2 NOT NULL
                    CONSTRAINT DF_Notices_date_created DEFAULT (SYSUTCDATETIME()),
                date_updated datetime2 NULL,
                created_by_user_code int NULL,
                modified_by_user_code int NULL,
                is_deleted bit NOT NULL
                    CONSTRAINT DF_Notices_is_deleted DEFAULT (0),
                CONSTRAINT PK_Notices PRIMARY KEY CLUSTERED (notice_id)
            );
        END;

        IF OBJECT_ID(N'dbo.NoticeSchedule', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.NoticeSchedule
            (
                notice_schedule_id int IDENTITY(1, 1) NOT NULL,
                notice_id int NOT NULL,
                title_field varchar(500) NULL,
                start_date datetime2 NULL,
                end_date datetime2 NULL,
                sort_order int NULL,
                date_created datetime2 NOT NULL
                    CONSTRAINT DF_NoticeSchedule_date_created DEFAULT (SYSUTCDATETIME()),
                date_updated datetime2 NULL,
                created_by_user_code int NULL,
                modified_by_user_code int NULL,
                is_deleted bit NOT NULL
                    CONSTRAINT DF_NoticeSchedule_is_deleted DEFAULT (0),
                CONSTRAINT PK_NoticeSchedule PRIMARY KEY CLUSTERED (notice_schedule_id)
            );
        END;

        IF OBJECT_ID(N'dbo.journal_detail', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.journal_detail
            (
                journal_detail_id int IDENTITY(1, 1) NOT NULL,
                journal_detail_code uniqueidentifier NOT NULL
                    CONSTRAINT DF_journal_detail_code DEFAULT (NEWID()),
                journal_code bigint NULL,
                department_code smallint NOT NULL,
                site_code smallint NULL,
                vmf_code int NULL,
                journal_detail_type_code tinyint NOT NULL,
                journal_detail_isdebit bit NOT NULL
                    CONSTRAINT DF_journal_detail_isdebit DEFAULT (1),
                journal_detail_quantity decimal(18, 2) NOT NULL,
                journal_detail_tariff decimal(19, 5) NOT NULL,
                journal_detail_amount money NOT NULL,
                journal_detail_description char(32) NOT NULL,
                journal_detail_reversalof uniqueidentifier NULL,
                journal_detail_date_created smalldatetime NOT NULL
                    CONSTRAINT DF_journal_detail_date_created DEFAULT (GETDATE()),
                journal_detail_date_updated smalldatetime NULL,
                journal_detail_date_posted smalldatetime NULL,
                journal_detail_isaccepted bit NOT NULL
                    CONSTRAINT DF_journal_detail_isaccepted DEFAULT (0),
                journal_detail_financial_year char(4) NULL,
                journal_detail_date smalldatetime NULL,
                journal_detail_date_approved smalldatetime NULL,
                journal_detail_rebill_code uniqueidentifier NULL,
                journal_detail_isreversaldenied bit NULL,
                journal_detail_debitamount AS
                    (CASE journal_detail_isdebit
                        WHEN 1 THEN journal_detail_amount
                        ELSE -journal_detail_amount
                     END),
                journal_detail_note varchar(max) NULL,
                journal_detail_inactive bit NULL,
                journal_detail_inactive_date smalldatetime NULL,
                CONSTRAINT PK_journal_detail PRIMARY KEY CLUSTERED (journal_detail_id),
                CONSTRAINT IX_journal_detail_UNIQUE UNIQUE (journal_detail_code)
            );
        END;
        """;
}
