/*
  Finance legacy parity inventory — READ ONLY

  Run this against the restored client FIS database while on site. It creates
  no database objects, executes no Finance procedure, and never starts a
  transaction. Set @IncludeResultMetadata = 1 only when the login may read
  result metadata; it still does not execute a procedure.

  This is evidence gathering, not a deployment or a fallback mechanism.
*/
SET NOCOUNT ON;

DECLARE @SchemaName sysname = N'dbo';
DECLARE @IncludeResultMetadata bit = 0;

DECLARE @ExpectedProcedures TABLE
(
    [ProcedureName] sysname NOT NULL PRIMARY KEY,
    [ModuleSlice] nvarchar(80) NOT NULL,
    [Operation] nvarchar(24) NOT NULL
);

INSERT INTO @ExpectedProcedures ([ProcedureName], [ModuleSlice], [Operation])
VALUES
    (N'DEV_SEL_DepartmentForBasImporter', N'BAS allocation', N'select'),
    (N'DEV_SEL_ValidateBasImport', N'BAS allocation', N'select'),
    (N'DEV_INS_SegmentFromXml', N'BAS allocation', N'mutation'),
    (N'DEV_UPD_SegmentActiveFromXml', N'BAS allocation', N'mutation'),
    (N'DEV_SEL_SegmentType', N'BAS allocation', N'select'),
    (N'DEV_REP_JournalsWithInvalidBASCodes', N'BAS allocation', N'select'),
    (N'DEV_UPD_FixJournalWithInvalidBASCodes', N'BAS allocation', N'mutation'),
    (N'DEV_REP_VehicleJournalBASCodesMap', N'BAS allocation', N'select'),
    (N'DEV_INS_VehicleJournalSegmentMap', N'BAS allocation', N'mutation'),
    (N'DEV_SEL_FinancialYears', N'Finance selectors', N'select'),
    (N'DEV_SEL_BatchLookup', N'Finance selectors', N'select'),
    (N'DEV_SEL_Departments', N'Finance selectors', N'select'),
    (N'DEV_SEL_Sites', N'Finance selectors', N'select'),
    (N'DEV_SEL_Provinces', N'Finance selectors', N'select'),
    (N'DEV_REP_DetailedInvoicedReport', N'Invoice reports', N'select'),
    (N'DEV_REP_SummaryInvoiceByJournalDetailType', N'Invoice reports', N'select'),
    (N'DEV_REP_DetailedInvoicedVIPandTaxiReport', N'Invoice reports', N'select'),
    (N'DEV_REP_FuelDetailedInvoicedReport', N'Invoice reports', N'select'),
    (N'DEV_REP_DetailedInvoicedTollAndOil', N'Invoice reports', N'select'),
    (N'DEV_REP_SurchargeDetailedInvoicedReport', N'Invoice reports', N'select'),
    (N'DEV_REP_VehicleBillingHistory', N'Financial reports', N'select'),
    (N'DEV_REP_PreviousYearsIncomeSplit', N'Financial reports', N'select'),
    (N'DEV_REP_InvoicedAmountsPerMonth', N'Financial reports', N'select'),
    (N'DEV_REP_InvoicedAmountsPerMonthPerSite', N'Financial reports', N'select'),
    (N'DEV_REP_InvoicedAmountsPerMonthPerSitePerVehicle', N'Financial reports', N'select'),
    (N'DEV_REP_ViewReversalTreeFromJournalNumber', N'Financial reports', N'select'),
    (N'DEV_REP_ExpenditureToDatePerDepartment', N'Outstanding amounts', N'select'),
    (N'DEV_REP_ALLOutstandingAmountsPerDepartment', N'Outstanding amounts', N'select'),
    (N'DEV_REP_ALLOutstandingAmountsPerDepartmentAndSite', N'Outstanding amounts', N'select'),
    (N'DEV_REP_ALLOutstandingAmountsAtMonthEndPerVehicle', N'Outstanding amounts', N'select'),
    (N'DEV_REP_AllocationException', N'Outstanding amounts', N'select'),
    (N'DEV_REP_CompareBilledKilosAndFuelConsumption', N'Missing kilometres', N'select'),
    (N'DEV_REP_AllVehiclesWithNoKilosButConsumingFuel', N'Missing kilometres', N'select'),
    (N'DEV_REP_WesbankExpensesPerProvinceAndMonth', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesPerProvincePerDepartmentAndMonth', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesPerProvincePerDepartmentSiteAndMonth', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesOneProvinceAndAllMonths', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesOneProvinceAllDepartmentSubTotalAndMonth', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonth', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesPerProvinceAllDepartmentSiteAndMonthDetailedFuel', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesPerProvinceAllDepartmentSiteAndMonthDetailedOther', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonthDetailedFuel', N'Wesbank', N'select'),
    (N'DEV_REP_WesbankExpensesOneProvinceAllDepartmentSiteAndMonthDetailedOther', N'Wesbank', N'select'),
    (N'DEV_REP_SummaryReport', N'Regional finance', N'select'),
    (N'DEV_REP_SummaryReportDeptCostType', N'Regional finance', N'select'),
    (N'DEV_REP_SummaryReportByCostType', N'Regional finance', N'select'),
    (N'DEV_REP_SummaryReportPerProvince', N'Regional finance', N'select'),
    (N'DEV_REP_SummaryReportPerProvinceDeptCostType', N'Regional finance', N'select'),
    (N'DEV_REP_SummaryReportPerProvinceByCostType', N'Regional finance', N'select'),
    (N'DEV_REP_ELSAuditTrailReport', N'Audit trail', N'select'),
    (N'DEV_REP_ManualKilosAuditTrailReport', N'Audit trail', N'select'),
    (N'DEV_REP_VehicleContractsAuditTrailReport', N'Audit trail', N'select'),
    (N'DEV_REP_VIPTAXIAuditTrailReport', N'Audit trail', N'select'),
    (N'DEV_REP_IncomeVsExpensesVIPPool', N'Profitability', N'select'),
    (N'DEV_REP_SiteVehicleDetail', N'Wesbank drill-down', N'select'),
    (N'DEV_REP_RegistrationNumberDetail', N'Wesbank drill-down', N'select'),
    (N'DEV_REP_VehicleDetail', N'Wesbank drill-down', N'select'),
    (N'DEV_REP_TotalCostPerProvince', N'Regional drill-down', N'select'),
    (N'DEV_REP_TotalCostPerProvincePerDepartment', N'Regional drill-down', N'select'),
    (N'DEV_REP_TotalCostPerProvincePerDepartmentPerSite', N'Regional drill-down', N'select'),
    (N'DEV_REP_TotalCostPerProvincePerDepartmentPerSitePerCostType', N'Regional drill-down', N'select');

DECLARE @ExpectedViews TABLE ([ViewName] sysname NOT NULL PRIMARY KEY);
INSERT INTO @ExpectedViews ([ViewName]) VALUES (N'ReversalTree');

/* 1. Procedure presence, ownership and freshness. */
SELECT
    expected.[ModuleSlice],
    expected.[Operation],
    expected.[ProcedureName],
    CASE WHEN procedureObject.[object_id] IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    procedureObject.[create_date],
    procedureObject.[modify_date]
FROM @ExpectedProcedures AS expected
LEFT JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = @SchemaName
LEFT JOIN [sys].[procedures] AS procedureObject
    ON procedureObject.[schema_id] = schemaObject.[schema_id]
   AND procedureObject.[name] = expected.[ProcedureName]
ORDER BY expected.[ModuleSlice], expected.[Operation], expected.[ProcedureName];

/* 2. Exact parameter names, types, direction and defaults for every present procedure. */
SELECT
    expected.[ModuleSlice],
    expected.[Operation],
    expected.[ProcedureName],
    parameterObject.[parameter_id],
    parameterObject.[name] AS [ParameterName],
    typeObject.[name] AS [SqlType],
    parameterObject.[max_length],
    parameterObject.[precision],
    parameterObject.[scale],
    parameterObject.[is_output],
    parameterObject.[has_default_value],
    parameterObject.[default_value]
FROM @ExpectedProcedures AS expected
INNER JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = @SchemaName
INNER JOIN [sys].[procedures] AS procedureObject
    ON procedureObject.[schema_id] = schemaObject.[schema_id]
   AND procedureObject.[name] = expected.[ProcedureName]
LEFT JOIN [sys].[parameters] AS parameterObject
    ON parameterObject.[object_id] = procedureObject.[object_id]
LEFT JOIN [sys].[types] AS typeObject
    ON typeObject.[user_type_id] = parameterObject.[user_type_id]
ORDER BY expected.[ModuleSlice], expected.[ProcedureName], parameterObject.[parameter_id];

/* 3. Required view evidence and its dependencies. */
SELECT
    expected.[ViewName],
    CASE WHEN viewObject.[object_id] IS NULL THEN N'MISSING' ELSE N'PRESENT' END AS [Status],
    viewObject.[create_date],
    viewObject.[modify_date]
FROM @ExpectedViews AS expected
LEFT JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = @SchemaName
LEFT JOIN [sys].[views] AS viewObject
    ON viewObject.[schema_id] = schemaObject.[schema_id]
   AND viewObject.[name] = expected.[ViewName]
ORDER BY expected.[ViewName];

/* 4. Direct dependencies of expected procedures/views, including unresolved references. */
;WITH [ExpectedObjects] AS
(
    SELECT procedureObject.[object_id], expected.[ModuleSlice], expected.[ProcedureName] AS [ObjectName]
    FROM @ExpectedProcedures AS expected
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = @SchemaName
    INNER JOIN [sys].[procedures] AS procedureObject
        ON procedureObject.[schema_id] = schemaObject.[schema_id]
       AND procedureObject.[name] = expected.[ProcedureName]

    UNION ALL

    SELECT viewObject.[object_id], N'Financial reports', expected.[ViewName]
    FROM @ExpectedViews AS expected
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = @SchemaName
    INNER JOIN [sys].[views] AS viewObject
        ON viewObject.[schema_id] = schemaObject.[schema_id]
       AND viewObject.[name] = expected.[ViewName]
)
SELECT
    expected.[ModuleSlice],
    expected.[ObjectName],
    referencedSchema.[name] AS [ReferencedSchema],
    referencedObject.[name] AS [ReferencedObject],
    dependency.[referenced_entity_name] AS [UnresolvedReferencedEntity],
    dependency.[is_ambiguous],
    dependency.[is_caller_dependent]
FROM [ExpectedObjects] AS expected
LEFT JOIN [sys].[sql_expression_dependencies] AS dependency
    ON dependency.[referencing_id] = expected.[object_id]
LEFT JOIN [sys].[objects] AS referencedObject
    ON referencedObject.[object_id] = dependency.[referenced_id]
LEFT JOIN [sys].[schemas] AS referencedSchema
    ON referencedSchema.[schema_id] = referencedObject.[schema_id]
ORDER BY expected.[ModuleSlice], expected.[ObjectName], referencedSchema.[name], referencedObject.[name];

/* 5. Trigger inventory for the Finance tables touched by the legacy flows. */
SELECT
    tableSchema.[name] AS [TableSchema],
    tableObject.[name] AS [TableName],
    triggerObject.[name] AS [TriggerName],
    triggerObject.[is_disabled],
    triggerObject.[is_instead_of_trigger],
    triggerObject.[create_date],
    triggerObject.[modify_date]
FROM [sys].[tables] AS tableObject
INNER JOIN [sys].[schemas] AS tableSchema
    ON tableSchema.[schema_id] = tableObject.[schema_id]
LEFT JOIN [sys].[triggers] AS triggerObject
    ON triggerObject.[parent_id] = tableObject.[object_id]
WHERE tableSchema.[name] = @SchemaName
  AND tableObject.[name] IN
  (
      N'journal_detail', N'journal', N'batch', N'financial_year', N'vehicle_master',
      N'contract', N'site', N'wesbank_transaction'
  )
ORDER BY tableObject.[name], triggerObject.[name];

/* 6. Optional result-column metadata. SQL Server describes metadata without running the procedure. */
IF @IncludeResultMetadata = 1
BEGIN
    SELECT
        expected.[ModuleSlice],
        expected.[ProcedureName],
        resultColumn.[column_ordinal],
        resultColumn.[name] AS [ColumnName],
        resultColumn.[system_type_name] AS [SqlType],
        resultColumn.[is_nullable],
        resultColumn.[error_number],
        resultColumn.[error_message]
    FROM @ExpectedProcedures AS expected
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = @SchemaName
    INNER JOIN [sys].[procedures] AS procedureObject
        ON procedureObject.[schema_id] = schemaObject.[schema_id]
       AND procedureObject.[name] = expected.[ProcedureName]
    CROSS APPLY [sys].[dm_exec_describe_first_result_set_for_object](procedureObject.[object_id], 0) AS resultColumn
    WHERE expected.[Operation] = N'select'
    ORDER BY expected.[ModuleSlice], expected.[ProcedureName], resultColumn.[column_ordinal];
END;

/*
  Do not execute DEV_INS_* or DEV_UPD_* from this script. Record the parameter
  result above, then compare it with the original Web Forms call site before
  any mutation is attempted in a client session.
*/
