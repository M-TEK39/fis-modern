/*
  System mutation parity inventory — READ ONLY

  Run only against the restored client database. This script issues catalog
  SELECT statements only: it neither starts a transaction nor executes a
  stored procedure, trigger, or application mutation.

  It is deliberately scoped to the high-risk legacy-write paths currently
  exposed by the modern API: Contracts, Vehicle Master, Fines, Logbooks,
  Logsheets, Trips/Routes, Job Cards, Lease Tariffs, and Finance journals.
*/
SET NOCOUNT ON;

DECLARE @MutationTargets TABLE
(
    [MutationTargetId] int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    [Module] nvarchar(80) NOT NULL,
    [ModernMutation] nvarchar(160) NOT NULL,
    [SchemaName] sysname NOT NULL DEFAULT N'dbo',
    [TableName] sysname NULL,
    [LegacyProcedure] sysname NULL,
    [LegacyTrigger] sysname NULL
);

INSERT INTO @MutationTargets
    ([Module], [ModernMutation], [TableName], [LegacyProcedure], [LegacyTrigger])
VALUES
    (N'Contracts', N'Create a pre-created contract', N'contract', N'DEV_INS_Contract2', N'TRG_INS_CheckDuplicateContract'),
    (N'Contracts', N'Create or submit a contract for approval', N'contract', N'DEV_INS_Contract_New_ForApproval', N'TRG_INS_ContractJournalDetailRecord'),
    (N'Contracts', N'Edit or resubmit a pending contract', N'contract', N'DEV_UPD_Contract_New_ApprovalPhase', N'TRG_UPD_ContractJournalDetailRecord'),
    (N'Contracts', N'Approve, decline, or cancel a pending contract', N'contract', N'DEV_UPD_Contract_New_ApproveDeclineOrCancel', N'TRG_Audit_Contract_Update'),
    (N'Contracts', N'Approve or decline a backdating request', N'contract', N'DEV_UPD_Contract_BackDating_RequestedApproveDecline', NULL),
    (N'Contracts', N'Activate a contract', N'contract', N'DEV_UPD_Contract_NewActivate', N'TRG_UPD_ContractJournalDetailRecord'),
    (N'Contracts', N'Extend a contract', N'contract', N'DEV_UPD_Contract_ExtendExisting', N'TRG_Audit_Contract_Update'),
    (N'Contracts', N'Reassign a contract', N'contract', N'DEV_UPD_Contract_ReassignExisting', N'TRG_INS_UpdateContractChargedUntil'),
    (N'Contracts', N'Write contract status history', N'contract_status_history', N'DEV_UPD_ContractStatusHistory', NULL),
    (N'Contracts', N'Delete a contract', N'contract', NULL, N'TRG_DEL_Contract'),
    (N'Contracts', N'Delete a contract', N'contract', NULL, N'TRG_Audit_Contract_Delete'),
    (N'Vehicle Master', N'Capture a pre-vehicle', N'pre_vehicle_master', N'DEV_INS_New_Vehicle_Master', N'TRG_Audit_Pre_Vehicle_Master_Insert'),
    (N'Vehicle Master', N'Authorise a pre-vehicle', N'pre_vehicle_master', N'DEV_INS_VehicleFromPre_Vehicle_Master', N'TRG_Audit_Pre_Vehicle_Master_Update'),
    (N'Vehicle Master', N'Reject a pre-vehicle', N'pre_vehicle_master', N'DEV_UPD_Rejected_PreVehicles', N'TRG_Audit_Pre_Vehicle_Master_Update'),
    (N'Vehicle Master', N'Print and clear a pre-vehicle authorisation listing', N'pre_vehicle_master', N'DEV_CLR_NewVehicleFromAuthList', NULL),
    (N'Vehicle Master', N'Capture vehicle notes, extras, damages, and maintenance settings', N'pre_vehicle_master', N'DEV_INS_PreVehicle_master_Notes', N'TRG_Audit_Pre_Vehicle_Master_Update'),
    (N'Vehicle Master', N'Capture vehicle notes, extras, damages, and maintenance settings', N'pre_vehicle_master', N'DEV_INS_temp_fleet_notes', NULL),
    (N'Vehicle Master', N'Capture vehicle notes, extras, damages, and maintenance settings', N'pre_vehicle_master', N'DEV_INS_NewVehicle_Extras', N'TRG_Audit_Pre_Vehicle_Master_Delete'),
    (N'Vehicle Master', N'Capture vehicle notes, extras, damages, and maintenance settings', N'Vehicle_Damages', N'DEV_INS_Vehicle_Damages', NULL),
    (N'Vehicle Master', N'Capture vehicle notes, extras, damages, and maintenance settings', N'vehicle_master', N'DEV_UPD_VehicleMaintenanceOptions', N'TRG_Audit_Vehicle_Master_Update'),
    (N'Vehicle Master', N'Authorise a pre-vehicle maintenance plan', N'Vehicle_Maintenance', N'DEV_UPD_MaintenanceVmfCode', NULL),
    (N'Vehicle Master', N'Edit or delete vehicle master', N'vehicle_master', NULL, N'TRG_Audit_Vehicle_Master_Delete'),
    (N'Vehicle Master', N'Edit or delete vehicle master', N'vehicle_master', NULL, N'trg_del_preventvehicledeletion'),
    (N'Fines', N'Create, update, or delete a fine', N'Fines', NULL, N'TRG_Audit_Fines_Insert'),
    (N'Fines', N'Create, update, or delete a fine', N'Fines', NULL, N'TRG_Audit_Fines_Update'),
    (N'Fines', N'Create, update, or delete a fine', N'Fines', NULL, N'TRG_Audit_Fines_Delete'),
    (N'Fines', N'Create, update, or delete a traffic department', N'Traffic_Dept', NULL, NULL),
    (N'Logbooks', N'Issue, update, return, or delete a logbook', N'logbook', NULL, NULL),
    (N'Logsheets', N'Create a manual logsheet', N'Logsheets', NULL, N'TRG_INS_LogsheetJournalDetailRecord'),
    (N'Logsheets', N'Create or update a manual logsheet', N'Logsheets', NULL, N'TRG_INS_UPD_Logsheet_Check_Overlap'),
    (N'Logsheets', N'Create or update a manual logsheet', N'Logsheets', NULL, N'TRG_INS_UPD_Logsheet_CheckOverlappingOpenELsTrip'),
    (N'Logsheets', N'Update a manual logsheet', N'Logsheets', NULL, N'TRG_UPD_LogsheetJournalDetailRecord'),
    (N'Logsheets', N'Delete a manual logsheet', N'Logsheets', NULL, N'TRG_DEL_Logsheet'),
    (N'Trips and Routes', N'Create a trip authority and routes', N'trip_authorities', N'DEV_INS_TripXML', NULL),
    (N'Trips and Routes', N'Update a trip authority and routes', N'trip_authorities', N'DEV_UPD_TripXML', NULL),
    (N'Trips and Routes', N'Close a trip authority and routes', N'route_details', N'DEV_UPD_TripXMLForClosingOfTrip', N'TRG_INS_RouteJournalDetailRecord'),
    (N'Trips and Routes', N'Renew a trip authority and routes', N'trip_authorities', N'DEV_UPD_TripXMLForRenewalOfTrip', NULL),
    (N'Trips and Routes', N'Create or amend route details', N'route_details', N'DEV_INS_RouteDetails2', N'TRG_INS_RouteJournalDetailRecord'),
    (N'Trips and Routes', N'Create or amend route details', N'route_details', NULL, N'TRG_INS_UPD_CheckOverLapping_RouteDetailsKilos'),
    (N'Trips and Routes', N'Create or amend route details', N'route_details', NULL, N'TRG_INS_UPD_RouteDetails_CheckOverLapping_ManualLogsheets'),
    (N'Trips and Routes', N'Find or bulk-delete trips without routes', N'trip_authorities', N'DEV_SEL_TripsWithoutRoutes', NULL),
    (N'Trips and Routes', N'Find or bulk-delete trips without routes', N'trip_authorities', N'ADM_DEL_TripsWithoutRoutes', NULL),
    (N'Job Cards', N'Create a job card (fallback only when create procedure is absent)', N'JobCard', N'DEV_INS_NewJobCards', NULL),
    (N'Job Cards', N'Assign, start, or close a job card', N'JobCard', N'DEV_UPD_JobCard', NULL),
    (N'Job Cards', N'Authorise or decline a job card', N'JobCard', N'DEV_UPD_JobcardAuthorisersUpdates', NULL),
    (N'Job Cards', N'Cancel a job card', N'JobCard', N'DEV_UPD_JobcardCancelRequest', NULL),
    (N'Job Cards', N'Delete a job card', N'JobCard', N'DEV_DEL_Jobcard', NULL),
    (N'Job Cards', N'Mark job cards in progress', N'JobCard', N'DEV_UPD_JobcardUpdateStatusToInProgress', NULL),
    (N'Job Cards', N'Amend post-close costs', N'JobCard', NULL, NULL),
    (N'Lease Tariffs', N'Import a lease-tariff file', N'LeaseTariff_File', N'ADM_IMPORT_LeaseTariffFile', NULL),
    (N'Lease Tariffs', N'Import or recalculate lease tariffs', N'LeaseTariff', N'ADM_UPD_LeaseFixedTariff', N'TRG_UPSERT_Vehicle_Tariff'),
    (N'Lease Tariffs', N'Create a lease tariff', N'LeaseTariff', N'DEV_INS_LeaseTariff', N'TRG_Audit_LeaseTariff_Insert'),
    (N'Lease Tariffs', N'Extend the latest lease tariff period', N'LeaseTariff', N'DEV_UPD_LeaseTariffData', N'TRG_Audit_LeaseTariff_Update'),
    (N'Lease Tariffs', N'No legacy user-facing delete flow', N'LeaseTariff', NULL, N'TRG_Audit_LeaseTariff_Delete'),
    (N'Lease Contract Terms', N'Capture or resubmit lease contract terms', N'LeaseContractTerms', N'DEV_UPD_LeaseContractTerms', NULL),
    (N'Lease Contract Terms', N'Authorise lease contract terms', N'LeaseContractTerms', N'DEV_UPD_LeaseContractTermsAuthorisation', NULL),
    (N'Lease Contract Terms', N'Reject lease contract terms', N'LeaseContractTerms', N'DEV_UPD_LeaseContractTermsRejection', NULL),
    (N'Lease Contract Terms', N'Recall a lease contract term', N'LeaseContractTerms', N'DEV_UPD_LeaseContractTermsRecall', NULL),
    (N'Lease Contract Terms', N'Change recalled lease contract-term status', N'LeaseContractTerms', N'DEV_UPD_LeaseContractTermsAuthorityStatus', NULL),
    (N'Lease Contract Terms', N'Write a lease contract-term comment', N'LeaseContractTermsComment', N'DEV_INS_LeaseContractTermsComments', NULL),
    (N'Lease Contract Terms', N'Write a lease contract-term comment (archived procedure name)', N'LeaseContractTermsComment', N'DEV_INS_Comments', NULL),
    (N'Finance Journals', N'Create a batch or close kilometre gaps', N'journal_detail', N'DEV_INS_Batch', N'TRG_UPD_AllocationExceptionJournalDetailRecord'),
    (N'Finance Journals', N'Create a batch or close kilometre gaps', N'journal_detail', N'DEV_INS_CloseKiloGapsFromXML', N'trg_upd_checkvehiclejournalrecords'),
    (N'Finance Batch', N'Read or change a batch workflow parameter', NULL, N'DEV_SEL_ParameterValue', NULL),
    (N'Finance Batch', N'Read or change a batch workflow parameter', NULL, N'DEV_UPD_ParameterValue', NULL),
    (N'Finance Batch', N'Start a batch and take the site offline', NULL, N'ADM_TriggerBatchJob', NULL),
    (N'Finance Batch', N'Roll back a batch', NULL, N'ADM_TriggerRollbackJob', NULL),
    (N'Finance Batch', N'Check SCOA before finishing a batch', NULL, N'ADM_CheckSCOA_Version5', NULL),
    (N'Finance Batch', N'Check batch job state and recorded logs', NULL, N'ADM_CheckJobStatus', NULL),
    (N'Finance Batch', N'Check batch job state and recorded logs', NULL, N'ADM_CheckRecordedLogs', NULL);

INSERT INTO @MutationTargets
    ([Module], [ModernMutation], [SchemaName], [TableName], [LegacyProcedure], [LegacyTrigger])
VALUES
    (N'Finance Tariff Parameters', N'Initiate, update, approve, or reject fiscal tariff parameters', N'fin', N'TariffParameter', N'DEV_UPD_TariffParameter', NULL),
    (N'Finance Tariff Parameters', N'Update a maintenance value for a fiscal tariff parameter', N'fin', N'MaintenanceValue', N'DEV_UPD_MaintenanceValue', NULL),
    (N'Finance Tariff Parameters', N'Update an overhead for a fiscal tariff parameter', N'fin', N'Overhead', N'DEV_UPD_Overhead', NULL);

DECLARE @ExpectedProcedureParameters TABLE
(
    [Module] nvarchar(80) NOT NULL,
    [SchemaName] sysname NOT NULL DEFAULT N'dbo',
    [LegacyProcedure] sysname NOT NULL,
    [ParameterOrdinal] int NOT NULL,
    [ParameterName] sysname NOT NULL,
    [SourceEvidence] nvarchar(240) NOT NULL
);

/* Parameter names/order proven by the archived legacy callers. A missing or
   renamed live parameter is a verification failure, not permission to use
   the modern direct-DML implementation. */
INSERT INTO @ExpectedProcedureParameters
    ([Module], [LegacyProcedure], [ParameterOrdinal], [ParameterName], [SourceEvidence])
VALUES
    (N'Lease Tariffs', N'DEV_INS_LeaseTariff', 1, N'@vmf_code', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb AddVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_INS_LeaseTariff', 2, N'@start_date', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb AddVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_INS_LeaseTariff', 3, N'@end_date', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb AddVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_INS_LeaseTariff', 4, N'@fixed_tariff', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb AddVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_INS_LeaseTariff', 5, N'@excess_kilo_tariff', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb AddVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_UPD_LeaseTariffData', 1, N'@leaseCode', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb ExtendVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_UPD_LeaseTariffData', 2, N'@newEndDate', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb ExtendVehicleLeaseTariff'),
    (N'Lease Tariffs', N'DEV_UPD_LeaseTariffData', 3, N'@typeUpdate', N'GGFIS_DataAccessLayer/Vehicle_Profile.vb ExtendVehicleLeaseTariff'),
    (N'Job Cards', N'DEV_INS_NewJobCards', 1, N'@GGNumber', N'JobcardClasses/Changes.vb InsertNewJobCards'),
    (N'Job Cards', N'DEV_INS_NewJobCards', 2, N'@extraCode', N'JobcardClasses/Changes.vb InsertNewJobCards'),
    (N'Job Cards', N'DEV_INS_NewJobCards', 3, N'@CaptureBy', N'JobcardClasses/Changes.vb InsertNewJobCards'),
    (N'Job Cards', N'DEV_UPD_JobCard', 1, N'@VmfCode', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 2, N'@extra_code', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 3, N'@Status_comment', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 4, N'@AssignedTo', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 5, N'@AssignedDate', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 6, N'@Damages', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 7, N'@comments', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 8, N'@Status_code', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 9, N'@UserID', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 10, N'@barcode', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobCard', 11, N'@dateclosed', N'Restored client catalog DEV_UPD_JobCard'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 1, N'@ggnumber', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 2, N'@extra_code', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 3, N'@priority', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 4, N'@Authoriser', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 5, N'@comment', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 6, N'@reviewed', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 7, N'@JobCardtatus', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 8, N'@AssignedTo', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardAuthorisersUpdates', 9, N'@AssignedDate', N'Restored client catalog DEV_UPD_JobcardAuthorisersUpdates'),
    (N'Job Cards', N'DEV_UPD_JobcardCancelRequest', 1, N'@jcnumber', N'jobcards2/classes/capturerControl.vb UpdateJobcardCancelation'),
    (N'Job Cards', N'DEV_UPD_JobcardCancelRequest', 2, N'@userid', N'jobcards2/classes/capturerControl.vb UpdateJobcardCancelation'),
    (N'Job Cards', N'DEV_UPD_JobcardUpdateStatusToInProgress', 1, N'@ggnumber', N'JobcardClasses/Changes.vb UpdateJobcardsStatusInProgress'),
    (N'Job Cards', N'DEV_UPD_JobcardUpdateStatusToInProgress', 2, N'@jcnumber', N'JobcardClasses/Changes.vb UpdateJobcardsStatusInProgress'),
    (N'Job Cards', N'DEV_DEL_Jobcard', 1, N'@vmf_code', N'jobcards2/classes/capturerControl.vb deleteJobcards'),
    (N'Job Cards', N'DEV_DEL_Jobcard', 2, N'@extraDescription', N'jobcards2/classes/capturerControl.vb deleteJobcards'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 1, N'@ContractCode', N'GGFIS_DataAccessLayer/Contract.vb ExtendExisting'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 2, N'@VMFCode', N'GGFIS_DataAccessLayer/Contract.vb ExtendExisting'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 3, N'@TargetReturnDate', N'GGMT.Database/v2.1.08 DEV_UPD_Contract_ExtendExisting'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 4, N'@contract_estimated_overall_km', N'GGMT.Database/v2.1.08 DEV_UPD_Contract_ExtendExisting'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 5, N'@Notes', N'GGMT.Database/v2.1.08 DEV_UPD_Contract_ExtendExisting'),
    (N'Contracts', N'DEV_UPD_Contract_ExtendExisting', 6, N'@UserID', N'GGMT.Database/v2.1.08 DEV_UPD_Contract_ExtendExisting'),
    (N'Trips and Routes', N'DEV_UPD_TripXMLForClosingOfTrip', 1, N'@Trip', N'GGFIS_DataAccessLayer/Trips.vb CloseTrip'),
    (N'Trips and Routes', N'DEV_UPD_TripXMLForClosingOfTrip', 2, N'@XmlDocument', N'GGFIS_DataAccessLayer/Trips.vb CloseTrip'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 1, N'@TariffParameterID', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 2, N'@TariffParameterYear', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 3, N'@AnnualInterestRatePercentage', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 4, N'@AnnualPayments', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 5, N'@PoolVehicleChargedDaysPerMonth', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 6, N'@CostCategoryMultiple', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 7, N'@AnnualRecoveredKilos', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 8, N'@AverageFuelPrice', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 9, N'@EffectiveDate', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 10, N'@user_access_code', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Tariff Parameters', N'DEV_UPD_TariffParameter', 11, N'@Approved', N'GGFIS_DataAccessLayer/Finance.vb TariffParameter_ApprovedYear'),
    (N'Finance Batch', N'DEV_SEL_ParameterValue', 1, N'@receivedParameterName', N'GGFIS_DataAccessLayer/GenericDB.vb GetParameterValue'),
    (N'Finance Batch', N'DEV_UPD_ParameterValue', 1, N'@parameterName', N'GGFIS_DataAccessLayer/GenericDB.vb SetParameterValue'),
    (N'Finance Batch', N'DEV_UPD_ParameterValue', 2, N'@parameterValue', N'GGFIS_DataAccessLayer/GenericDB.vb SetParameterValue'),
    (N'Finance Batch', N'ADM_TriggerBatchJob', 1, N'@BatchDate', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb StartBatch'),
    (N'Finance Batch', N'ADM_TriggerBatchJob', 2, N'@TriggerUser', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb StartBatch'),
    (N'Finance Batch', N'ADM_TriggerBatchJob', 3, N'@JobName', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb StartBatch'),
    (N'Finance Batch', N'ADM_TriggerBatchJob', 4, N'@StepId', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb StartBatch'),
    (N'Finance Batch', N'ADM_TriggerRollbackJob', 1, N'@Location', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb RollbackBatch'),
    (N'Finance Batch', N'ADM_TriggerRollbackJob', 2, N'@User', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb RollbackBatch'),
    (N'Finance Batch', N'ADM_TriggerRollbackJob', 3, N'@JobName', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb RollbackBatch'),
    (N'Finance Batch', N'ADM_TriggerRollbackJob', 4, N'@StepId', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb RollbackBatch'),
    (N'Finance Batch', N'ADM_CheckJobStatus', 1, N'@JobName', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb GetJobDuration'),
    (N'Finance Batch', N'ADM_CheckRecordedLogs', 1, N'@LogJob', N'GGFIS_DataAccessLayer/BatchManagementFunctions.vb GetLatestLogs');

UPDATE @ExpectedProcedureParameters
SET [SchemaName] = N'fin'
WHERE [Module] = N'Finance Tariff Parameters'
  AND [LegacyProcedure] = N'DEV_UPD_TariffParameter';

/* 1. One row per modern mutation and required legacy table/procedure/trigger. */
SELECT
    target.[Module],
    target.[ModernMutation],
    target.[TableName],
    target.[LegacyProcedure],
    CASE WHEN procedureObject.[object_id] IS NULL AND target.[LegacyProcedure] IS NOT NULL THEN N'MISSING'
         WHEN target.[LegacyProcedure] IS NULL THEN N'NOT APPLICABLE'
         ELSE N'PRESENT' END AS [ProcedureStatus],
    target.[LegacyTrigger],
    CASE WHEN triggerObject.[object_id] IS NULL AND target.[LegacyTrigger] IS NOT NULL THEN N'MISSING'
         WHEN target.[LegacyTrigger] IS NULL THEN N'NOT KNOWN FROM SOURCE'
         WHEN triggerObject.[is_disabled] = 1 THEN N'DISABLED'
         ELSE N'ENABLED' END AS [TriggerStatus],
    tableObject.[object_id] AS [TableObjectId]
FROM @MutationTargets AS target
LEFT JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = target.[SchemaName]
LEFT JOIN [sys].[tables] AS tableObject
    ON tableObject.[schema_id] = schemaObject.[schema_id]
   AND tableObject.[name] = target.[TableName]
LEFT JOIN [sys].[procedures] AS procedureObject
    ON procedureObject.[schema_id] = schemaObject.[schema_id]
   AND procedureObject.[name] = target.[LegacyProcedure]
LEFT JOIN [sys].[triggers] AS triggerObject
    ON triggerObject.[parent_id] = tableObject.[object_id]
   AND triggerObject.[name] = target.[LegacyTrigger]
ORDER BY target.[Module], target.[ModernMutation], target.[TableName], target.[LegacyProcedure], target.[LegacyTrigger];

/* 2. Exact live parameter contract for every expected stored procedure. */
SELECT DISTINCT
    target.[Module],
    target.[ModernMutation],
    target.[LegacyProcedure],
    parameterObject.[parameter_id],
    parameterObject.[name] AS [ParameterName],
    typeObject.[name] AS [SqlType],
    parameterObject.[max_length],
    parameterObject.[precision],
    parameterObject.[scale],
    parameterObject.[is_output],
    parameterObject.[has_default_value],
    parameterObject.[default_value]
FROM @MutationTargets AS target
INNER JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = target.[SchemaName]
INNER JOIN [sys].[procedures] AS procedureObject
    ON procedureObject.[schema_id] = schemaObject.[schema_id]
   AND procedureObject.[name] = target.[LegacyProcedure]
LEFT JOIN [sys].[parameters] AS parameterObject
    ON parameterObject.[object_id] = procedureObject.[object_id]
LEFT JOIN [sys].[types] AS typeObject
    ON typeObject.[user_type_id] = parameterObject.[user_type_id]
ORDER BY target.[Module], target.[ModernMutation], target.[LegacyProcedure], parameterObject.[parameter_id];

/* 2b. Parameter names/order expected from the original caller source. */
SELECT
    expected.[Module],
    expected.[SchemaName],
    expected.[LegacyProcedure],
    expected.[ParameterOrdinal],
    expected.[ParameterName] AS [ExpectedParameterName],
    parameterObject.[name] AS [LiveParameterName],
    typeObject.[name] AS [LiveSqlType],
    CASE
        WHEN procedureObject.[object_id] IS NULL THEN N'PROCEDURE MISSING'
        WHEN parameterObject.[parameter_id] IS NULL THEN N'PARAMETER MISSING'
        WHEN parameterObject.[name] <> expected.[ParameterName] THEN N'PARAMETER NAME MISMATCH'
        ELSE N'MATCHED'
    END AS [VerificationStatus],
    expected.[SourceEvidence]
FROM @ExpectedProcedureParameters AS expected
LEFT JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = expected.[SchemaName]
LEFT JOIN [sys].[procedures] AS procedureObject
    ON procedureObject.[schema_id] = schemaObject.[schema_id]
   AND procedureObject.[name] = expected.[LegacyProcedure]
LEFT JOIN [sys].[parameters] AS parameterObject
    ON parameterObject.[object_id] = procedureObject.[object_id]
   AND parameterObject.[parameter_id] = expected.[ParameterOrdinal]
LEFT JOIN [sys].[types] AS typeObject
    ON typeObject.[user_type_id] = parameterObject.[user_type_id]
ORDER BY expected.[Module], expected.[LegacyProcedure], expected.[ParameterOrdinal];

/* 3. Trigger event/enablement state and direct table binding. */
SELECT DISTINCT
    target.[Module],
    target.[ModernMutation],
    tableObject.[name] AS [TableName],
    triggerObject.[name] AS [TriggerName],
    triggerObject.[is_disabled],
    triggerObject.[is_instead_of_trigger],
    triggerEvent.[type_desc] AS [TriggerEvent],
    triggerObject.[create_date],
    triggerObject.[modify_date]
FROM @MutationTargets AS target
INNER JOIN [sys].[schemas] AS schemaObject
    ON schemaObject.[name] = target.[SchemaName]
INNER JOIN [sys].[tables] AS tableObject
    ON tableObject.[schema_id] = schemaObject.[schema_id]
   AND tableObject.[name] = target.[TableName]
LEFT JOIN [sys].[triggers] AS triggerObject
    ON triggerObject.[parent_id] = tableObject.[object_id]
   AND (target.[LegacyTrigger] IS NULL OR triggerObject.[name] = target.[LegacyTrigger])
LEFT JOIN [sys].[trigger_events] AS triggerEvent
    ON triggerEvent.[object_id] = triggerObject.[object_id]
ORDER BY target.[Module], target.[ModernMutation], tableObject.[name], triggerObject.[name], triggerEvent.[type_desc];

/* 4. Direct database dependencies of the procedures and triggers above. */
;WITH [ExpectedModules] AS
(
    SELECT DISTINCT procedureObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyProcedure] AS [ObjectName]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[procedures] AS procedureObject
        ON procedureObject.[schema_id] = schemaObject.[schema_id]
       AND procedureObject.[name] = target.[LegacyProcedure]

    UNION ALL

    SELECT DISTINCT triggerObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyTrigger]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[tables] AS tableObject
        ON tableObject.[schema_id] = schemaObject.[schema_id]
       AND tableObject.[name] = target.[TableName]
    INNER JOIN [sys].[triggers] AS triggerObject
        ON triggerObject.[parent_id] = tableObject.[object_id]
       AND triggerObject.[name] = target.[LegacyTrigger]
)
SELECT
    expected.[Module],
    expected.[ModernMutation],
    expected.[ObjectName],
    referencedSchema.[name] AS [ReferencedSchema],
    referencedObject.[name] AS [ReferencedObject],
    referencedObject.[type_desc] AS [ReferencedObjectType],
    dependency.[referenced_entity_name] AS [UnresolvedReferencedEntity],
    dependency.[is_ambiguous],
    dependency.[is_caller_dependent]
FROM [ExpectedModules] AS expected
LEFT JOIN [sys].[sql_expression_dependencies] AS dependency
    ON dependency.[referencing_id] = expected.[object_id]
LEFT JOIN [sys].[objects] AS referencedObject
    ON referencedObject.[object_id] = dependency.[referenced_id]
LEFT JOIN [sys].[schemas] AS referencedSchema
    ON referencedSchema.[schema_id] = referencedObject.[schema_id]
ORDER BY expected.[Module], expected.[ModernMutation], expected.[ObjectName], referencedSchema.[name], referencedObject.[name];

/* 5. Legacy views reached by the expected procedure or trigger graph. */
;WITH [ExpectedModules] AS
(
    SELECT DISTINCT procedureObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyProcedure] AS [ObjectName]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[procedures] AS procedureObject
        ON procedureObject.[schema_id] = schemaObject.[schema_id]
       AND procedureObject.[name] = target.[LegacyProcedure]

    UNION ALL

    SELECT DISTINCT triggerObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyTrigger]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[tables] AS tableObject
        ON tableObject.[schema_id] = schemaObject.[schema_id]
       AND tableObject.[name] = target.[TableName]
    INNER JOIN [sys].[triggers] AS triggerObject
        ON triggerObject.[parent_id] = tableObject.[object_id]
       AND triggerObject.[name] = target.[LegacyTrigger]
)
SELECT DISTINCT
    expected.[Module],
    expected.[ModernMutation],
    expected.[ObjectName] AS [ReferencingObject],
    viewSchema.[name] AS [ViewSchema],
    viewObject.[name] AS [ViewName],
    viewObject.[is_ms_shipped],
    viewObject.[create_date],
    viewObject.[modify_date],
    moduleDefinition.[definition] AS [ViewDefinition]
FROM [ExpectedModules] AS expected
INNER JOIN [sys].[sql_expression_dependencies] AS dependency
    ON dependency.[referencing_id] = expected.[object_id]
INNER JOIN [sys].[views] AS viewObject
    ON viewObject.[object_id] = dependency.[referenced_id]
INNER JOIN [sys].[schemas] AS viewSchema
    ON viewSchema.[schema_id] = viewObject.[schema_id]
LEFT JOIN [sys].[sql_modules] AS moduleDefinition
    ON moduleDefinition.[object_id] = viewObject.[object_id]
ORDER BY expected.[Module], expected.[ModernMutation], expected.[ObjectName], viewSchema.[name], viewObject.[name];

/* 6. Deployed source definitions for direct comparison with archived SQL. */
;WITH [ExpectedModules] AS
(
    SELECT DISTINCT procedureObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyProcedure] AS [ObjectName]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[procedures] AS procedureObject
        ON procedureObject.[schema_id] = schemaObject.[schema_id]
       AND procedureObject.[name] = target.[LegacyProcedure]

    UNION ALL

    SELECT DISTINCT triggerObject.[object_id], target.[Module], target.[ModernMutation], target.[LegacyTrigger]
    FROM @MutationTargets AS target
    INNER JOIN [sys].[schemas] AS schemaObject
        ON schemaObject.[name] = target.[SchemaName]
    INNER JOIN [sys].[tables] AS tableObject
        ON tableObject.[schema_id] = schemaObject.[schema_id]
       AND tableObject.[name] = target.[TableName]
    INNER JOIN [sys].[triggers] AS triggerObject
        ON triggerObject.[parent_id] = tableObject.[object_id]
       AND triggerObject.[name] = target.[LegacyTrigger]
)
SELECT DISTINCT
    expected.[Module],
    expected.[ModernMutation],
    expected.[ObjectName],
    objectDefinition.[type_desc] AS [ObjectType],
    objectDefinition.[modify_date],
    moduleDefinition.[definition] AS [Definition]
FROM [ExpectedModules] AS expected
INNER JOIN [sys].[objects] AS objectDefinition
    ON objectDefinition.[object_id] = expected.[object_id]
LEFT JOIN [sys].[sql_modules] AS moduleDefinition
    ON moduleDefinition.[object_id] = expected.[object_id]
ORDER BY expected.[Module], expected.[ModernMutation], expected.[ObjectName];

/*
  Do not use this inventory as proof of side effects. The follow-up test must
  compare legacy and modern mutations against a designated test record inside
  a transaction that is explicitly rolled back by the operator.
*/
