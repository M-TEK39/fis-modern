using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes the third-party rental workflow against both the original
/// schema and the expanded schema. The legacy tables remain authoritative when
/// the optional modern supplier/allocation objects are not present.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed table and column compatibility allowlists; values are parameters."
)]
public sealed class ThirdPartyRentalRepository : IThirdPartyRentalRepository
{
    private const string ModernSupplierTable = "Suppliers";
    private const string LegacySupplierTable = "Third_party_rental";
    private const string VehicleSourceTable = "vehicle_source";
    private const string ProjectTable = "third_party_projects";
    private const string ModernAllocationTable = "third_party_allocations";
    private const string LegacyAllocationTable = "Third_Party_Vehicle_Allocations";
    private const string LegacyProjectSupplierTable = "third_party_project_suppliers";
    private const string ExpandedProjectSupplierTable = "Third_Party_Project_Supplier";
    private const string LegacyVehicleTable = "Third_Party_Vehicles";

    private readonly FisDbContext _context;

    public ThirdPartyRentalRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ThirdPartySupplierRecord>> GetSuppliersAsync(
        CancellationToken cancellationToken = default
    )
    {
        var modern = await GetSchemaAsync(ModernSupplierTable, cancellationToken);
        if (
            modern is not null
            && modern.Has("supplier_id")
            && modern.First("supplier_name", "name") is not null
        )
        {
            var rows = await QueryModernSuppliersAsync(modern, cancellationToken);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        return await QueryLegacySuppliersAsync(cancellationToken);
    }

    public async Task<ThirdPartySupplierRecord?> GetSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default
    )
    {
        var modern = await GetSchemaAsync(ModernSupplierTable, cancellationToken);
        if (
            modern is not null
            && modern.Has("supplier_id")
            && modern.First("supplier_name", "name") is not null
        )
        {
            var row = (
                await QueryModernSuppliersAsync(
                    modern,
                    cancellationToken,
                    "[supplier_id] = @supplierId",
                    command => AddParameter(command, "@supplierId", DbType.Int32, supplierId)
                )
            ).SingleOrDefault();
            if (row is not null)
            {
                return row;
            }
        }

        return (
            await QueryLegacySuppliersAsync(
                cancellationToken,
                "[tr].[third_party_id] = @supplierId",
                command => AddParameter(command, "@supplierId", DbType.Int32, supplierId)
            )
        ).SingleOrDefault();
    }

    public async Task<ThirdPartySupplierRecord> CreateSupplierAsync(
        ThirdPartySupplierWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        ValidateSupplier(input);
        var modern = await GetSchemaAsync(ModernSupplierTable, cancellationToken);
        if (
            modern is not null
            && modern.Has("supplier_id")
            && modern.First("supplier_name", "name") is not null
        )
        {
            var id = await InTransactionAsync(
                (connection, transaction, token) =>
                    InsertModernSupplierAsync(
                        modern,
                        input,
                        currentUserId,
                        connection,
                        transaction,
                        token
                    ),
                cancellationToken
            );
            return await GetSupplierOrThrowAsync(id, cancellationToken);
        }

        var rental =
            await GetSchemaAsync(LegacySupplierTable, cancellationToken)
            ?? throw new InvalidOperationException(
                "Legacy third-party supplier storage is not available in this database."
            );
        var source =
            await GetSchemaAsync(VehicleSourceTable, cancellationToken)
            ?? throw new InvalidOperationException(
                "Legacy vehicle source storage is not available in this database."
            );
        var type = await GetSchemaAsync("type", cancellationToken);
        var mapping = await GetSchemaAsync("Contract_Type_Group_Mapping", cancellationToken);
        var typeCode = await ResolveTypeCodeAsync(input.service_code, type, cancellationToken);
        if (
            await FindLegacySupplierIdAsync(rental, source, typeCode, input.name, cancellationToken)
            is not null
        )
        {
            throw new ArgumentException(
                "A supplier with this name and service already exists.",
                nameof(input)
            );
        }
        var idLegacy = await InTransactionAsync(
            (connection, transaction, token) =>
                InsertLegacySupplierAsync(
                    input,
                    typeCode,
                    rental,
                    source,
                    mapping,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
        return await GetSupplierOrThrowAsync(idLegacy, cancellationToken);
    }

    public async Task<ThirdPartySupplierRecord> UpdateSupplierAsync(
        int supplierId,
        ThirdPartySupplierWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        ValidateSupplier(input);
        if (!input.active && await HasSupplierProjectUsageAsync(supplierId, cancellationToken))
        {
            throw new ArgumentException(
                "The supplier cannot be made inactive while it is assigned to a project.",
                nameof(input)
            );
        }
        var modern = await GetSchemaAsync(ModernSupplierTable, cancellationToken);
        if (
            modern is not null
            && modern.Has("supplier_id")
            && modern.First("supplier_name", "name") is not null
        )
        {
            var existing = await GetSupplierAsync(supplierId, cancellationToken);
            if (existing is not null)
            {
                await InTransactionAsync(
                    (connection, transaction, token) =>
                        UpdateModernSupplierAsync(
                            modern,
                            supplierId,
                            input,
                            currentUserId,
                            connection,
                            transaction,
                            token
                        ),
                    cancellationToken
                );
                return await GetSupplierOrThrowAsync(supplierId, cancellationToken);
            }
        }

        var rental =
            await GetSchemaAsync(LegacySupplierTable, cancellationToken)
            ?? throw new KeyNotFoundException($"Third-party supplier {supplierId} was not found.");
        var source =
            await GetSchemaAsync(VehicleSourceTable, cancellationToken)
            ?? throw new InvalidOperationException(
                "Legacy vehicle source storage is not available in this database."
            );
        var type = await GetSchemaAsync("type", cancellationToken);
        var mapping = await GetSchemaAsync("Contract_Type_Group_Mapping", cancellationToken);
        var typeCode = await ResolveTypeCodeAsync(input.service_code, type, cancellationToken);
        await InTransactionAsync(
            (connection, transaction, token) =>
                UpdateLegacySupplierAsync(
                    supplierId,
                    input,
                    typeCode,
                    rental,
                    source,
                    mapping,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
        return await GetSupplierOrThrowAsync(supplierId, cancellationToken);
    }

    public async Task<IReadOnlyList<ThirdPartyServiceOption>> GetServiceOptionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var schema = await GetSchemaAsync("type", cancellationToken);
        if (
            schema is null
            || !schema.Has("type_code")
            || schema.First("type_description", "description") is null
        )
        {
            return Array.Empty<ThirdPartyServiceOption>();
        }

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "code", "smallint", "type_code")},
                   {Projection(schema, "name", "varchar(100)", "type_description", "description")}
            FROM [dbo].[{schema.Table}]
            WHERE LOWER({Column(schema, "type_description", "description")}) NOT IN
                ('not defined', 'pool vehicle', 'vip services', 'permanent hire', 'lease')
            ORDER BY {Column(schema, "type_description", "description")}
            """;

        var options = new List<ThirdPartyServiceOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = ReadInt32(reader, "code");
            var name = ReadString(reader, "name");
            if (code is not null && !string.IsNullOrWhiteSpace(name))
            {
                options.Add(new ThirdPartyServiceOption(code.Value, name));
            }
        }

        return options;
    }

    public async Task<IReadOnlyList<ThirdPartyProjectRecord>> GetProjectsAsync(
        CancellationToken cancellationToken = default
    ) => await QueryProjectsAsync(cancellationToken);

    public async Task<IReadOnlyList<ThirdPartyProjectRecord>> GetProjectsByDepartmentAsync(
        short departmentCode,
        CancellationToken cancellationToken = default
    ) =>
        await QueryProjectsAsync(
            cancellationToken,
            $"{ColumnForRequired(await GetProjectSchemaOrNullAsync(cancellationToken), "department_code", "Department_Code")} = @departmentCode",
            command => AddParameter(command, "@departmentCode", DbType.Int16, departmentCode)
        );

    public async Task<ThirdPartyProjectRecord?> GetProjectAsync(
        int projectId,
        CancellationToken cancellationToken = default
    )
    {
        var schema = await GetProjectSchemaOrNullAsync(cancellationToken);
        if (schema is null)
        {
            return null;
        }

        return (
            await QueryProjectsAsync(
                cancellationToken,
                $"{ColumnForRequired(schema, "project_id", "Project_id")} = @projectId",
                command => AddParameter(command, "@projectId", DbType.Int32, projectId)
            )
        ).SingleOrDefault();
    }

    public async Task<ThirdPartyProjectRecord> CreateProjectAsync(
        ThirdPartyProjectWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        ValidateProject(input);
        var schema =
            await GetProjectSchemaOrNullAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Third-party project storage is not available in this database."
            );

        var id = await InTransactionAsync(
            (connection, transaction, token) =>
                InsertProjectAsync(schema, input, currentUserId, connection, transaction, token),
            cancellationToken
        );
        return await GetProjectOrThrowAsync(id, cancellationToken);
    }

    public async Task<ThirdPartyProjectRecord> UpdateProjectAsync(
        int projectId,
        ThirdPartyProjectWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        ValidateProject(input);
        var schema =
            await GetProjectSchemaOrNullAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Third-party project storage is not available in this database."
            );
        if (await GetProjectAsync(projectId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Third-party project {projectId} was not found.");
        }

        await InTransactionAsync(
            (connection, transaction, token) =>
                UpdateProjectAsync(
                    schema,
                    projectId,
                    input,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
        return await GetProjectOrThrowAsync(projectId, cancellationToken);
    }

    public async Task DeleteProjectAsync(
        int projectId,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        var schema =
            await GetProjectSchemaOrNullAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Third-party project storage is not available in this database."
            );
        await InTransactionAsync(
            (connection, transaction, token) =>
                DeleteRecordAsync(
                    schema,
                    "project_id",
                    "Project_id",
                    projectId,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ThirdPartyAllocationRecord>> GetAllocationsByProjectAsync(
        int projectId,
        CancellationToken cancellationToken = default
    )
    {
        var modern = await GetSchemaAsync(ModernAllocationTable, cancellationToken);
        if (modern is not null && modern.Has("allocation_id") && modern.Has("project_id"))
        {
            var rows = await QueryModernAllocationsAsync(
                modern,
                cancellationToken,
                "[project_id] = @projectId",
                command => AddParameter(command, "@projectId", DbType.Int32, projectId)
            );
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        var legacy = await GetSchemaAsync(LegacyAllocationTable, cancellationToken);
        if (legacy is null || !legacy.Has("Third_Party_Vehicle_AllocationsID"))
        {
            return Array.Empty<ThirdPartyAllocationRecord>();
        }

        return await QueryLegacyAllocationsAsync(
            legacy,
            projectId,
            cancellationToken,
            "[Third_Party_ProjectID] = @projectId",
            command => AddParameter(command, "@projectId", DbType.Int32, projectId)
        );
    }

    public async Task<ThirdPartyAllocationRecord> CreateAllocationAsync(
        ThirdPartyAllocationWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        if (input.project_id <= 0)
        {
            throw new ArgumentException("A project is required.", nameof(input));
        }

        var modern = await GetSchemaAsync(ModernAllocationTable, cancellationToken);
        if (modern is not null && modern.Has("allocation_id") && modern.Has("project_id"))
        {
            var id = await InTransactionAsync(
                (connection, transaction, token) =>
                    InsertModernAllocationAsync(
                        modern,
                        input,
                        currentUserId,
                        connection,
                        transaction,
                        token
                    ),
                cancellationToken
            );
            return await GetAllocationOrThrowAsync(id, input.project_id, cancellationToken);
        }

        var legacy =
            await GetSchemaAsync(LegacyAllocationTable, cancellationToken)
            ?? throw new InvalidOperationException(
                "Third-party vehicle allocation storage is not available in this database."
            );
        var projectSupplier = await GetFirstSchemaAsync(
            cancellationToken,
            LegacyProjectSupplierTable,
            ExpandedProjectSupplierTable
        );
        var idLegacy = await InTransactionAsync(
            (connection, transaction, token) =>
                InsertLegacyAllocationAsync(
                    legacy,
                    projectSupplier,
                    input,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
        return await GetAllocationOrThrowAsync(idLegacy, input.project_id, cancellationToken);
    }

    public async Task DeleteAllocationAsync(
        int allocationId,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        var modern = await GetSchemaAsync(ModernAllocationTable, cancellationToken);
        if (modern is not null && modern.Has("allocation_id"))
        {
            await InTransactionAsync(
                (connection, transaction, token) =>
                    DeleteRecordAsync(
                        modern,
                        "allocation_id",
                        "allocation_id",
                        allocationId,
                        currentUserId,
                        connection,
                        transaction,
                        token
                    ),
                cancellationToken
            );
            return;
        }

        var legacy =
            await GetSchemaAsync(LegacyAllocationTable, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Third-party allocation {allocationId} was not found."
            );
        await InTransactionAsync(
            (connection, transaction, token) =>
                DeleteRecordAsync(
                    legacy,
                    "allocation_id",
                    "Third_Party_Vehicle_AllocationsID",
                    allocationId,
                    currentUserId,
                    connection,
                    transaction,
                    token
                ),
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<ThirdPartyVehicleRecord>> GetVehiclesBySupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default
    )
    {
        var vehicleMaster = await GetSchemaAsync("vehicle_master", cancellationToken);
        if (
            vehicleMaster is not null
            && vehicleMaster.Has("vmf_code")
            && vehicleMaster.Has("supplier_id")
        )
        {
            var modernRows = await QueryVehiclesAsync(
                vehicleMaster,
                cancellationToken,
                "[supplier_id] = @supplierId",
                command => AddParameter(command, "@supplierId", DbType.Int32, supplierId)
            );
            if (modernRows.Count > 0)
            {
                return modernRows;
            }
        }

        var legacy = await GetSchemaAsync(LegacyVehicleTable, cancellationToken);
        if (legacy is null || !legacy.Has("Third_Party_Vehicle_ID"))
        {
            return Array.Empty<ThirdPartyVehicleRecord>();
        }

        return await QueryVehiclesAsync(
            legacy,
            cancellationToken,
            "[Third_Party_Supplier_ID] = @supplierId",
            command => AddParameter(command, "@supplierId", DbType.Int32, supplierId),
            legacyVehicle: true
        );
    }

    public async Task<IReadOnlyList<ThirdPartyClassRequirementRecord>> GetClassRequirementsAsync(
        int projectId,
        CancellationToken cancellationToken = default
    )
    {
        var modern = await GetSchemaAsync("ClassRequirements", cancellationToken);
        if (modern is not null && modern.Has("project_id") && modern.Has("class_id"))
        {
            var rows = await QueryModernRequirementsAsync(modern, projectId, cancellationToken);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        var project = await GetProjectAsync(projectId, cancellationToken);
        if (string.IsNullOrWhiteSpace(project?.class_configuration))
        {
            return Array.Empty<ThirdPartyClassRequirementRecord>();
        }

        var names = await GetConfigurationNamesAsync(cancellationToken);
        var requirements = new List<ThirdPartyClassRequirementRecord>();
        foreach (
            var part in project.class_configuration.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var pieces = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (!int.TryParse(pieces.ElementAtOrDefault(0), out var classId) || classId <= 0)
            {
                continue;
            }

            int? requiredCount =
                pieces.Length > 1 && int.TryParse(pieces[1], out var count) ? count : null;
            requirements.Add(
                new ThirdPartyClassRequirementRecord(
                    classId,
                    names.GetValueOrDefault(classId) ?? $"Class {classId}",
                    requiredCount
                )
            );
        }

        return requirements;
    }

    private async Task<List<ThirdPartySupplierRecord>> QueryModernSuppliersAsync(
        Schema schema,
        CancellationToken cancellationToken,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "supplier_id", "int", "supplier_id")},
                   {Projection(schema, "name", "varchar(200)", "supplier_name", "name")},
                   {Projection(schema, "address", "varchar(500)", "address")},
                   {Projection(schema, "postal_address", "varchar(500)", "postal_address")},
                   {Projection(schema, "tel", "varchar(100)", "phone_number", "tel", "tel_number")},
                   {Projection(schema, "fax", "varchar(100)", "fax", "fax_number")},
                   {Projection(schema, "cell", "varchar(100)", "cell", "cell_number")},
                   {Projection(schema, "email", "varchar(200)", "email", "email_address")},
                   {Projection(schema, "contact_person", "varchar(200)", "contact_person")},
                   {Projection(schema, "notes", "varchar(500)", "notes", "Note")},
                   {Projection(
                schema,
                "service_code",
                "varchar(100)",
                "service_code",
                "supplier_type",
                "type_code"
            )},
                   {Projection(schema, "active", "bit", "is_active", "active")}
            FROM [dbo].[{schema.Table}]
            WHERE {ActivePredicate(schema)}
            {(string.IsNullOrWhiteSpace(predicate) ? string.Empty : $"AND ({predicate})")}
            ORDER BY {Column(schema, "supplier_name", "name", "supplier_id")}
            """;
        configure?.Invoke(command);
        return await ReadSuppliersAsync(command, cancellationToken);
    }

    private async Task<List<ThirdPartySupplierRecord>> QueryLegacySuppliersAsync(
        CancellationToken cancellationToken,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var rental = await GetSchemaAsync(LegacySupplierTable, cancellationToken);
        var source = await GetSchemaAsync(VehicleSourceTable, cancellationToken);
        if (
            rental is null
            || source is null
            || !rental.Has("third_party_id", "vs_code")
            || !source.Has("vs_code")
            || !source.Has("name")
        )
        {
            return new List<ThirdPartySupplierRecord>();
        }

        var type = await GetSchemaAsync("type", cancellationToken);
        var serviceProjection =
            type is null || type.First("type_description", "description") is null
                ? "CAST([tr].[type_code] AS varchar(100)) AS [service_code]"
                : $"CAST({QualifiedColumn(type, "t", "type_description", "description")} AS varchar(100)) AS [service_code]";
        var joinType = type is null
            ? string.Empty
            : $"LEFT JOIN [dbo].[{type.Table}] AS [t] ON [t].[{Column(type, "type_code")}] = [tr].[{Column(rental, "type_code")}]";

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT [tr].[{Column(rental, "third_party_id")}] AS [supplier_id],
                   [vs].[{Column(source, "name")}] AS [name],
                   {OptionalQualifiedProjection(
                source,
                "address",
                "varchar(500)",
                "physical_address",
                "address",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "postal_address",
                "varchar(500)",
                "postal_address",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "tel",
                "varchar(100)",
                "tel_number",
                "tel",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "fax",
                "varchar(100)",
                "fax_number",
                "fax",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "cell",
                "varchar(100)",
                "Cell_Number",
                "cell_number",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "email",
                "varchar(200)",
                "email_address",
                "email",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "contact_person",
                "varchar(200)",
                "contact_person",
                qualify: "vs"
            )},
                   {OptionalQualifiedProjection(
                source,
                "notes",
                "varchar(500)",
                "Note",
                "notes",
                qualify: "vs"
            )},
                   {serviceProjection},
                   CAST([tr].[{Column(rental, "active")}] AS bit) AS [active]
            FROM [dbo].[{rental.Table}] AS [tr]
            INNER JOIN [dbo].[{source.Table}] AS [vs]
                ON [vs].[{Column(source, "vs_code")}] = [tr].[{Column(rental, "vs_code")}]
            {joinType}
            WHERE ISNULL([tr].[{Column(rental, "active")}], 0) <> 0
            {(string.IsNullOrWhiteSpace(predicate) ? string.Empty : $"AND ({predicate})")}
            ORDER BY [vs].[{Column(source, "name")}]
            """;
        configure?.Invoke(command);
        return await ReadSuppliersAsync(command, cancellationToken);
    }

    private async Task<List<ThirdPartyProjectRecord>> QueryProjectsAsync(
        CancellationToken cancellationToken,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var schema = await GetProjectSchemaOrNullAsync(cancellationToken);
        if (schema is null)
        {
            return new List<ThirdPartyProjectRecord>();
        }

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "project_id", "int", "project_id", "Project_id")},
                   {Projection(
                schema,
                "department_code",
                "smallint",
                "department_code",
                "Department_Code"
            )},
                   {Projection(schema, "site_code", "smallint", "site_code", "Site_Code")},
                   {Projection(
                schema,
                "description",
                "varchar(255)",
                "description",
                "Project_Description"
            )},
                   {Projection(schema, "start_date", "date", "start_date", "Project_Start_Date")},
                   {Projection(schema, "end_date", "date", "end_date", "Project_End_Date")},
                   {Projection(
                schema,
                "responsible_person",
                "varchar(150)",
                "responsible_person",
                "Project_Responsible_Person"
            )},
                   {Projection(
                schema,
                "rp_physical_address",
                "varchar(255)",
                "rp_physical_address",
                "RP_Physical_Address"
            )},
                   {Projection(
                schema,
                "rp_postal_address",
                "varchar(255)",
                "rp_postal_address",
                "RP_Postal_Address"
            )},
                   {Projection(schema, "rp_tel", "varchar(50)", "rp_tel", "RP_TelNumber")},
                   {Projection(schema, "rp_fax", "varchar(50)", "rp_fax", "RP_FaxNumber")},
                   {Projection(schema, "rp_email", "varchar(150)", "rp_email", "RP_Email_Address")},
                   {Projection(schema, "rp_cell", "varchar(50)", "rp_cell", "RP_CellNumber")},
                   {Projection(schema, "notes", "varchar(500)", "notes", "Notes")},
                   {Projection(
                schema,
                "order_reference",
                "varchar(50)",
                "order_reference",
                "Client_OrderReference_Number"
            )},
                   {Projection(
                schema,
                "class_configuration",
                "varchar(255)",
                "class_configuration",
                "ClassConfiguration"
            )}
            FROM [dbo].[{schema.Table}]
            WHERE {ActivePredicate(schema)}
            {(string.IsNullOrWhiteSpace(predicate) ? string.Empty : $"AND ({predicate})")}
            ORDER BY {Column(schema, "project_id", "Project_id")} DESC
            """;
        configure?.Invoke(command);

        var projects = new List<ThirdPartyProjectRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(
                new ThirdPartyProjectRecord(
                    ReadInt32(reader, "project_id") ?? 0,
                    ReadInt16(reader, "department_code"),
                    ReadInt16(reader, "site_code"),
                    ReadString(reader, "description"),
                    ReadDateTime(reader, "start_date"),
                    ReadDateTime(reader, "end_date"),
                    ReadString(reader, "responsible_person"),
                    ReadString(reader, "rp_physical_address"),
                    ReadString(reader, "rp_postal_address"),
                    ReadString(reader, "rp_tel"),
                    ReadString(reader, "rp_fax"),
                    ReadString(reader, "rp_email"),
                    ReadString(reader, "rp_cell"),
                    ReadString(reader, "notes"),
                    ReadString(reader, "order_reference"),
                    ReadString(reader, "class_configuration")
                )
            );
        }

        return projects;
    }

    private async Task<List<ThirdPartyAllocationRecord>> QueryModernAllocationsAsync(
        Schema schema,
        CancellationToken cancellationToken,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "allocation_id", "int", "allocation_id")},
                   {Projection(schema, "project_id", "int", "project_id")},
                   {Projection(schema, "supplier_id", "int", "supplier_id")},
                   {Projection(schema, "vehicle_id", "int", "vehicle_id")},
                   {Projection(schema, "class_id", "int", "class_id")},
                   {Projection(schema, "quantity", "int", "quantity")}
            FROM [dbo].[{schema.Table}]
            WHERE {ActivePredicate(schema)}
            {(string.IsNullOrWhiteSpace(predicate) ? string.Empty : $"AND ({predicate})")}
            ORDER BY {Column(schema, "allocation_id")} DESC
            """;
        configure?.Invoke(command);
        return await ReadAllocationsAsync(command, cancellationToken);
    }

    private async Task<List<ThirdPartyAllocationRecord>> QueryLegacyAllocationsAsync(
        Schema schema,
        int projectId,
        CancellationToken cancellationToken,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(
                schema,
                "allocation_id",
                "int",
                "Third_Party_Vehicle_AllocationsID"
            )},
                   {Projection(schema, "project_id", "int", "Third_Party_ProjectID")},
                   {Projection(schema, "supplier_id", "int", "Third_Party_SupplierID")},
                   {Projection(schema, "vehicle_id", "int", "Third_Party_vs_code")},
                   CAST(NULL AS int) AS [class_id],
                   CAST(1 AS int) AS [quantity]
            FROM [dbo].[{schema.Table}]
            WHERE {(predicate ?? "1 = 1")}
            ORDER BY {Column(schema, "Third_Party_Vehicle_AllocationsID")} DESC
            """;
        _ = projectId;
        configure?.Invoke(command);
        return await ReadAllocationsAsync(command, cancellationToken);
    }

    private async Task<List<ThirdPartyVehicleRecord>> QueryVehiclesAsync(
        Schema schema,
        CancellationToken cancellationToken,
        string predicate,
        Action<DbCommand>? configure,
        bool legacyVehicle = false
    )
    {
        var idCandidates = legacyVehicle
            ? new[] { "Third_Party_Vehicle_ID" }
            : new[] { "vmf_code" };
        var registrationCandidates = legacyVehicle
            ? new[] { "RegistrationNumber", "registration_number" }
            : new[] { "registration_number" };
        var modelCandidates = legacyVehicle
            ? new[] { "Third_Party_Model_ID" }
            : new[] { "model_description", "model_desc" };
        var yearCandidates = legacyVehicle ? new[] { "ModelYear" } : new[] { "year_manufactured" };
        var chassisCandidates = legacyVehicle
            ? new[] { "ChassisNumber" }
            : new[] { "chassis_number" };

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "vehicle_id", "int", idCandidates)},
                   {Projection(
                schema,
                "registration_number",
                "varchar(100)",
                registrationCandidates
            )},
                   {Projection(schema, "model_description", "varchar(100)", modelCandidates)},
                   {Projection(schema, "model_year", "varchar(20)", yearCandidates)},
                   {Projection(schema, "chassis_number", "varchar(100)", chassisCandidates)}
            FROM [dbo].[{schema.Table}]
            WHERE {predicate}
            {(schema.Has("is_deleted") ? "AND ISNULL([is_deleted], 0) = 0" : string.Empty)}
            ORDER BY {Column(schema, idCandidates)}
            """;
        configure?.Invoke(command);

        var vehicles = new List<ThirdPartyVehicleRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            vehicles.Add(
                new ThirdPartyVehicleRecord(
                    ReadInt32(reader, "vehicle_id") ?? 0,
                    ReadString(reader, "registration_number"),
                    ReadString(reader, "model_description"),
                    ReadString(reader, "model_year"),
                    ReadString(reader, "chassis_number")
                )
            );
        }

        return vehicles;
    }

    private async Task<List<ThirdPartyClassRequirementRecord>> QueryModernRequirementsAsync(
        Schema schema,
        int projectId,
        CancellationToken cancellationToken
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "class_id", "int", "class_id")},
                   {Projection(schema, "required_count", "int", "required_count")}
            FROM [dbo].[{schema.Table}]
            WHERE [project_id] = @projectId
              AND {ActivePredicate(schema)}
            ORDER BY [class_id]
            """;
        AddParameter(command, "@projectId", DbType.Int32, projectId);

        var raw = new List<(int ClassId, int? RequiredCount)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var classId = ReadInt32(reader, "class_id");
            if (classId is not null)
            {
                raw.Add((classId.Value, ReadInt32(reader, "required_count")));
            }
        }

        var names = await GetClassNamesAsync(raw.Select(item => item.ClassId), cancellationToken);
        return raw.Select(item => new ThirdPartyClassRequirementRecord(
                item.ClassId,
                names.GetValueOrDefault(item.ClassId) ?? $"Class {item.ClassId}",
                item.RequiredCount
            ))
            .ToList();
    }

    private async Task<Dictionary<int, string>> GetClassNamesAsync(
        IEnumerable<int> classIds,
        CancellationToken cancellationToken
    )
    {
        var schema = await GetSchemaAsync("class", cancellationToken);
        if (schema is null || !schema.Has("class_code") || schema.First("description") is null)
        {
            return new Dictionary<int, string>();
        }

        var ids = classIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        var parameters = new List<string>();
        for (var index = 0; index < ids.Length; index++)
        {
            var name = $"@class{index}";
            parameters.Add(name);
            AddParameter(command, name, DbType.Int32, ids[index]);
        }

        command.CommandText = $"""
            SELECT {Projection(schema, "class_id", "int", "class_code")},
                   {Projection(schema, "name", "varchar(100)", "description")}
            FROM [dbo].[{schema.Table}]
            WHERE {Column(schema, "class_code")} IN ({string.Join(", ", parameters)})
            """;
        var names = new Dictionary<int, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = ReadInt32(reader, "class_id");
            var name = ReadString(reader, "name");
            if (id is not null && !string.IsNullOrWhiteSpace(name))
                names[id.Value] = name;
        }

        return names;
    }

    private async Task<Dictionary<int, string>> GetConfigurationNamesAsync(
        CancellationToken cancellationToken
    )
    {
        var schema = await GetSchemaAsync("new_class_configuration", cancellationToken);
        if (
            schema is null
            || schema.First("class_configuration_code") is null
            || schema.First("description") is null
        )
        {
            return new Dictionary<int, string>();
        }

        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT {Projection(schema, "class_id", "int", "class_configuration_code")},
                   {Projection(schema, "name", "varchar(100)", "description")}
            FROM [dbo].[{schema.Table}]
            """;
        var names = new Dictionary<int, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = ReadInt32(reader, "class_id");
            var name = ReadString(reader, "name");
            if (id is not null && !string.IsNullOrWhiteSpace(name))
                names[id.Value] = name;
        }

        return names;
    }

    private async Task<int> InsertModernSupplierAsync(
        Schema schema,
        ThirdPartySupplierWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var values = new List<WriteValue>();
        AddRequired(values, schema, "supplier_name", "name", "@name", DbType.String, input.name);
        AddOptional(values, schema, "address", "@address", DbType.String, input.address);
        AddOptional(
            values,
            schema,
            "postal_address",
            "@postalAddress",
            DbType.String,
            input.postal_address
        );
        AddOptional(values, schema, "phone_number", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "tel", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "tel_number", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "fax", "@fax", DbType.String, input.fax);
        AddOptional(values, schema, "fax_number", "@fax", DbType.String, input.fax);
        AddOptional(values, schema, "cell", "@cell", DbType.String, input.cell);
        AddOptional(values, schema, "cell_number", "@cell", DbType.String, input.cell);
        AddOptional(values, schema, "email", "@email", DbType.String, input.email);
        AddOptional(values, schema, "email_address", "@email", DbType.String, input.email);
        AddOptional(
            values,
            schema,
            "contact_person",
            "@contactPerson",
            DbType.String,
            input.contact_person
        );
        AddOptional(values, schema, "notes", "@notes", DbType.String, input.notes);
        AddOptional(values, schema, "Note", "@notes", DbType.String, input.notes);
        AddOptional(
            values,
            schema,
            "service_code",
            "@serviceCode",
            DbType.String,
            input.service_code,
            "supplier_type",
            "type_code"
        );
        AddOptional(values, schema, "is_active", "@active", DbType.Boolean, input.active);
        AddOptional(values, schema, "active", "@active", DbType.Boolean, input.active);
        AddAuditCreate(values, schema, currentUserId);
        return await ExecuteInsertAsync(
            schema,
            "supplier_id",
            values,
            connection,
            transaction,
            cancellationToken
        );
    }

    private async Task<int> InsertLegacySupplierAsync(
        ThirdPartySupplierWrite input,
        int typeCode,
        Schema rental,
        Schema source,
        Schema? mapping,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var sourceValues = new List<WriteValue>();
        AddRequired(sourceValues, source, "name", "name", "@name", DbType.String, input.name);
        AddOptional(
            sourceValues,
            source,
            "physical_address",
            "@address",
            DbType.String,
            input.address
        );
        AddOptional(
            sourceValues,
            source,
            "postal_address",
            "@postalAddress",
            DbType.String,
            input.postal_address
        );
        AddOptional(sourceValues, source, "tel_number", "@tel", DbType.String, input.tel);
        AddOptional(sourceValues, source, "fax_number", "@fax", DbType.String, input.fax);
        AddOptional(sourceValues, source, "email_address", "@email", DbType.String, input.email);
        AddOptional(
            sourceValues,
            source,
            "contact_person",
            "@contactPerson",
            DbType.String,
            input.contact_person
        );
        AddOptional(sourceValues, source, "Note", "@notes", DbType.String, input.notes);
        AddOptional(sourceValues, source, "Cell_Number", "@cell", DbType.String, input.cell);
        var sourceCode = await ExecuteInsertAsync(
            source,
            "vs_code",
            sourceValues,
            connection,
            transaction,
            cancellationToken
        );

        var rentalValues = new List<WriteValue>();
        AddRequired(
            rentalValues,
            rental,
            "vs_code",
            "vs_code",
            "@vsCode",
            DbType.Int32,
            sourceCode
        );
        AddRequired(
            rentalValues,
            rental,
            "type_code",
            "type_code",
            "@typeCode",
            DbType.Int16,
            typeCode
        );
        AddRequired(
            rentalValues,
            rental,
            "active",
            "active",
            "@active",
            DbType.Byte,
            input.active ? 1 : 0
        );
        var supplierId = await ExecuteInsertAsync(
            rental,
            "third_party_id",
            rentalValues,
            connection,
            transaction,
            cancellationToken
        );

        if (
            mapping is not null
            && mapping.HasAll("vs_code", "type_code", "ctg_code", "Is_Third_Party")
        )
        {
            var mappingValues = new List<WriteValue>();
            AddRequired(
                mappingValues,
                mapping,
                "vs_code",
                "vs_code",
                "@mappingVsCode",
                DbType.Int32,
                sourceCode
            );
            AddRequired(
                mappingValues,
                mapping,
                "type_code",
                "type_code",
                "@mappingTypeCode",
                DbType.Int16,
                typeCode
            );
            AddRequired(
                mappingValues,
                mapping,
                "ctg_code",
                "ctg_code",
                "@ctgCode",
                DbType.Int32,
                input.ctg_code
            );
            AddRequired(
                mappingValues,
                mapping,
                "Is_Third_Party",
                "is_third_party",
                "@isThirdParty",
                DbType.Boolean,
                input.is_third_party
            );
            await ExecuteInsertAsync(
                mapping,
                null,
                mappingValues,
                connection,
                transaction,
                cancellationToken
            );
        }

        _ = currentUserId;
        return supplierId;
    }

    private async Task UpdateModernSupplierAsync(
        Schema schema,
        int supplierId,
        ThirdPartySupplierWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var values = new List<WriteValue>();
        AddRequired(values, schema, "supplier_name", "name", "@name", DbType.String, input.name);
        AddOptional(values, schema, "address", "@address", DbType.String, input.address);
        AddOptional(
            values,
            schema,
            "postal_address",
            "@postalAddress",
            DbType.String,
            input.postal_address
        );
        AddOptional(values, schema, "phone_number", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "tel", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "tel_number", "@phone", DbType.String, input.tel);
        AddOptional(values, schema, "fax", "@fax", DbType.String, input.fax);
        AddOptional(values, schema, "fax_number", "@fax", DbType.String, input.fax);
        AddOptional(values, schema, "cell", "@cell", DbType.String, input.cell);
        AddOptional(values, schema, "cell_number", "@cell", DbType.String, input.cell);
        AddOptional(values, schema, "email", "@email", DbType.String, input.email);
        AddOptional(values, schema, "email_address", "@email", DbType.String, input.email);
        AddOptional(
            values,
            schema,
            "contact_person",
            "@contactPerson",
            DbType.String,
            input.contact_person
        );
        AddOptional(values, schema, "notes", "@notes", DbType.String, input.notes);
        AddOptional(values, schema, "Note", "@notes", DbType.String, input.notes);
        AddOptional(
            values,
            schema,
            "service_code",
            "@serviceCode",
            DbType.String,
            input.service_code,
            "supplier_type",
            "type_code"
        );
        AddOptional(values, schema, "is_active", "@active", DbType.Boolean, input.active);
        AddOptional(values, schema, "active", "@active", DbType.Boolean, input.active);
        AddAuditUpdate(values, schema, currentUserId);
        await ExecuteUpdateAsync(
            schema,
            "supplier_id",
            values,
            supplierId,
            connection,
            transaction,
            cancellationToken
        );
    }

    private async Task UpdateLegacySupplierAsync(
        int supplierId,
        ThirdPartySupplierWrite input,
        int typeCode,
        Schema rental,
        Schema source,
        Schema? mapping,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var sourceCode = await GetLegacySourceCodeAsync(
            rental,
            supplierId,
            connection,
            transaction,
            cancellationToken
        );
        if (sourceCode is null)
        {
            throw new KeyNotFoundException($"Third-party supplier {supplierId} was not found.");
        }

        var sourceValues = new List<WriteValue>();
        AddRequired(sourceValues, source, "name", "name", "@name", DbType.String, input.name);
        AddOptional(
            sourceValues,
            source,
            "physical_address",
            "@address",
            DbType.String,
            input.address
        );
        AddOptional(
            sourceValues,
            source,
            "postal_address",
            "@postalAddress",
            DbType.String,
            input.postal_address
        );
        AddOptional(sourceValues, source, "tel_number", "@tel", DbType.String, input.tel);
        AddOptional(sourceValues, source, "fax_number", "@fax", DbType.String, input.fax);
        AddOptional(sourceValues, source, "email_address", "@email", DbType.String, input.email);
        AddOptional(
            sourceValues,
            source,
            "contact_person",
            "@contactPerson",
            DbType.String,
            input.contact_person
        );
        AddOptional(sourceValues, source, "Note", "@notes", DbType.String, input.notes);
        AddOptional(sourceValues, source, "Cell_Number", "@cell", DbType.String, input.cell);
        await ExecuteUpdateAsync(
            source,
            "vs_code",
            sourceValues,
            sourceCode.Value,
            connection,
            transaction,
            cancellationToken
        );

        var rentalValues = new List<WriteValue>();
        AddRequired(
            rentalValues,
            rental,
            "type_code",
            "type_code",
            "@typeCode",
            DbType.Int16,
            typeCode
        );
        AddRequired(
            rentalValues,
            rental,
            "active",
            "active",
            "@active",
            DbType.Byte,
            input.active ? 1 : 0
        );
        await ExecuteUpdateAsync(
            rental,
            "third_party_id",
            rentalValues,
            supplierId,
            connection,
            transaction,
            cancellationToken
        );
        if (
            mapping is not null
            && mapping.HasAll("vs_code", "type_code", "ctg_code", "Is_Third_Party")
        )
        {
            var mappingValues = new List<WriteValue>();
            AddRequired(
                mappingValues,
                mapping,
                "type_code",
                "type_code",
                "@mappingTypeCode",
                DbType.Int16,
                typeCode
            );
            AddRequired(
                mappingValues,
                mapping,
                "ctg_code",
                "ctg_code",
                "@ctgCode",
                DbType.Int32,
                input.ctg_code
            );
            AddRequired(
                mappingValues,
                mapping,
                "Is_Third_Party",
                "is_third_party",
                "@isThirdParty",
                DbType.Boolean,
                input.is_third_party
            );
            await ExecuteUpdateAsync(
                mapping,
                "vs_code",
                mappingValues,
                sourceCode.Value,
                connection,
                transaction,
                cancellationToken
            );
        }
        _ = currentUserId;
    }

    private async Task<int> InsertProjectAsync(
        Schema schema,
        ThirdPartyProjectWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var values = BuildProjectValues(schema, input);
        AddAuditCreate(values, schema, currentUserId);
        return await ExecuteInsertAsync(
            schema,
            "project_id",
            values,
            connection,
            transaction,
            cancellationToken
        );
    }

    private async Task UpdateProjectAsync(
        Schema schema,
        int projectId,
        ThirdPartyProjectWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var values = BuildProjectValues(schema, input);
        AddAuditUpdate(values, schema, currentUserId);
        await ExecuteUpdateAsync(
            schema,
            "project_id",
            values,
            projectId,
            connection,
            transaction,
            cancellationToken
        );
    }

    private static List<WriteValue> BuildProjectValues(Schema schema, ThirdPartyProjectWrite input)
    {
        var values = new List<WriteValue>();
        AddRequired(
            values,
            schema,
            "department_code",
            "department_code",
            "@departmentCode",
            DbType.Int16,
            input.department_code,
            "Department_Code"
        );
        AddOptional(
            values,
            schema,
            "site_code",
            "@siteCode",
            DbType.Int16,
            input.site_code,
            "Site_Code"
        );
        AddRequired(
            values,
            schema,
            "description",
            "description",
            "@description",
            DbType.String,
            input.description,
            "Project_Description"
        );
        AddRequired(
            values,
            schema,
            "start_date",
            "startDate",
            "@startDate",
            DbType.Date,
            input.start_date,
            "Project_Start_Date"
        );
        AddRequired(
            values,
            schema,
            "end_date",
            "endDate",
            "@endDate",
            DbType.Date,
            input.end_date,
            "Project_End_Date"
        );
        AddOptional(
            values,
            schema,
            "responsible_person",
            "@responsiblePerson",
            DbType.String,
            input.responsible_person,
            "Project_Responsible_Person"
        );
        AddOptional(
            values,
            schema,
            "rp_physical_address",
            "@rpPhysicalAddress",
            DbType.String,
            input.rp_physical_address,
            "RP_Physical_Address"
        );
        AddOptional(
            values,
            schema,
            "rp_postal_address",
            "@rpPostalAddress",
            DbType.String,
            input.rp_postal_address,
            "RP_Postal_Address"
        );
        AddOptional(
            values,
            schema,
            "rp_tel",
            "@rpTel",
            DbType.String,
            input.rp_tel,
            "RP_TelNumber"
        );
        AddOptional(
            values,
            schema,
            "rp_fax",
            "@rpFax",
            DbType.String,
            input.rp_fax,
            "RP_FaxNumber"
        );
        AddOptional(
            values,
            schema,
            "rp_email",
            "@rpEmail",
            DbType.String,
            input.rp_email,
            "RP_Email_Address"
        );
        AddOptional(
            values,
            schema,
            "rp_cell",
            "@rpCell",
            DbType.String,
            input.rp_cell,
            "RP_CellNumber"
        );
        AddOptional(values, schema, "notes", "@notes", DbType.String, input.notes, "Notes");
        AddOptional(
            values,
            schema,
            "order_reference",
            "@orderReference",
            DbType.String,
            input.order_reference,
            "Client_OrderReference_Number"
        );
        AddOptional(
            values,
            schema,
            "class_configuration",
            "@classConfiguration",
            DbType.String,
            input.class_configuration,
            "ClassConfiguration"
        );
        return values;
    }

    private async Task<int> InsertModernAllocationAsync(
        Schema schema,
        ThirdPartyAllocationWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        var values = new List<WriteValue>();
        AddRequired(
            values,
            schema,
            "project_id",
            "project_id",
            "@projectId",
            DbType.Int32,
            input.project_id
        );
        AddOptional(values, schema, "supplier_id", "@supplierId", DbType.Int32, input.supplier_id);
        AddOptional(values, schema, "vehicle_id", "@vehicleId", DbType.Int32, input.vehicle_id);
        AddOptional(values, schema, "class_id", "@classId", DbType.Int32, input.class_id);
        AddOptional(values, schema, "quantity", "@quantity", DbType.Int32, input.quantity);
        AddAuditCreate(values, schema, currentUserId);
        return await ExecuteInsertAsync(
            schema,
            "allocation_id",
            values,
            connection,
            transaction,
            cancellationToken
        );
    }

    private async Task<int> InsertLegacyAllocationAsync(
        Schema schema,
        Schema? projectSupplier,
        ThirdPartyAllocationWrite input,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        if (
            input.supplier_id is not null
            && projectSupplier is not null
            && projectSupplier.Has("Third_Party_ProjectID", "Third_Party_SupplierID")
        )
        {
            await EnsureLegacyProjectSupplierLinkAsync(
                projectSupplier,
                input.project_id,
                input.supplier_id.Value,
                connection,
                transaction,
                cancellationToken
            );
        }

        var values = new List<WriteValue>();
        AddOptional(
            values,
            schema,
            "Third_Party_vs_code",
            "@vehicleId",
            DbType.Int32,
            input.vehicle_id
        );
        AddOptional(
            values,
            schema,
            "Third_Party_ProjectID",
            "@projectId",
            DbType.Int32,
            input.project_id
        );
        AddOptional(
            values,
            schema,
            "Third_Party_SupplierID",
            "@supplierId",
            DbType.Int32,
            input.supplier_id
        );
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "Legacy third-party allocation columns are not available."
            );
        }

        _ = currentUserId;
        return await ExecuteInsertAsync(
            schema,
            "Third_Party_Vehicle_AllocationsID",
            values,
            connection,
            transaction,
            cancellationToken
        );
    }

    private async Task EnsureLegacyProjectSupplierLinkAsync(
        Schema schema,
        int projectId,
        int supplierId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[{schema.Table}]
                WHERE [{schema.First("Third_Party_ProjectID")}] = @projectId
                  AND [{schema.First("Third_Party_SupplierID")}] = @supplierId)
            INSERT INTO [dbo].[{schema.Table}] ([{schema.First(
                "Third_Party_ProjectID"
            )}], [{schema.First("Third_Party_SupplierID")}])
            VALUES (@projectId, @supplierId)
            """;
        AddParameter(command, "@projectId", DbType.Int32, projectId);
        AddParameter(command, "@supplierId", DbType.Int32, supplierId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ResolveTypeCodeAsync(
        string? serviceCode,
        Schema? schema,
        CancellationToken cancellationToken
    )
    {
        if (int.TryParse(serviceCode, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        if (schema is null)
        {
            throw new InvalidOperationException(
                "Supplier service types are not available in this database."
            );
        }
        var description =
            schema.First("type_description", "description")
            ?? throw new InvalidOperationException(
                "Supplier service descriptions are not available in this database."
            );
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText =
            $"SELECT TOP (1) {Column(schema, "type_code")} FROM [dbo].[{schema.Table}] WHERE LOWER({description}) = LOWER(@serviceCode)";
        AddParameter(command, "@serviceCode", DbType.String, serviceCode ?? string.Empty);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value
            ? throw new InvalidOperationException("Select a valid supplier service.")
            : Convert.ToInt32(value);
    }

    private async Task<int?> GetLegacySourceCodeAsync(
        Schema rental,
        int supplierId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT [{rental.First("vs_code")}] FROM [dbo].[{rental.Table}] WHERE [{rental.First("third_party_id")}] = @supplierId";
        AddParameter(command, "@supplierId", DbType.Int32, supplierId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private async Task<int?> FindLegacySupplierIdAsync(
        Schema rental,
        Schema source,
        int typeCode,
        string name,
        CancellationToken cancellationToken
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (1) [tr].[{rental.First("third_party_id")}]
            FROM [dbo].[{rental.Table}] AS [tr]
            INNER JOIN [dbo].[{source.Table}] AS [vs]
                ON [vs].[{source.First("vs_code")}] = [tr].[{rental.First("vs_code")}]
            WHERE LOWER([vs].[{source.First("name")}]) = LOWER(@name)
              AND [tr].[{rental.First("type_code")}] = @typeCode
            """;
        AddParameter(command, "@name", DbType.String, name.Trim());
        AddParameter(command, "@typeCode", DbType.Int16, typeCode);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private async Task<bool> HasSupplierProjectUsageAsync(
        int supplierId,
        CancellationToken cancellationToken
    )
    {
        var modernAllocation = await GetSchemaAsync(ModernAllocationTable, cancellationToken);
        if (
            modernAllocation is not null
            && modernAllocation.HasAll("supplier_id", "project_id")
            && await ExistsAsync(
                modernAllocation,
                modernAllocation.First("supplier_id")!,
                supplierId,
                cancellationToken
            )
        )
        {
            return true;
        }

        var projectSupplier = await GetFirstSchemaAsync(
            cancellationToken,
            LegacyProjectSupplierTable,
            ExpandedProjectSupplierTable
        );
        return projectSupplier is not null
            && projectSupplier.HasAll("Third_Party_ProjectID", "Third_Party_SupplierID")
            && await ExistsAsync(
                projectSupplier,
                projectSupplier.First("Third_Party_SupplierID")!,
                supplierId,
                cancellationToken
            );
    }

    private async Task<bool> ExistsAsync(
        Schema schema,
        string column,
        int value,
        CancellationToken cancellationToken
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText =
            $"SELECT TOP (1) 1 FROM [dbo].[{schema.Table}] WHERE {ActivePredicate(schema)} AND [{column}] = @value";
        AddParameter(command, "@value", DbType.Int32, value);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task<int> ExecuteInsertAsync(
        Schema schema,
        string? keyName,
        IReadOnlyList<WriteValue> values,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = keyName is null
            ? $"INSERT INTO [dbo].[{schema.Table}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))}); SELECT CAST(SCOPE_IDENTITY() AS int);"
            : $"INSERT INTO [dbo].[{schema.Table}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[{schema.First(keyName)}] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private async Task ExecuteUpdateAsync(
        Schema schema,
        string logicalKey,
        IReadOnlyList<WriteValue> values,
        int key,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        if (values.Count == 0)
            return;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"UPDATE [dbo].[{schema.Table}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [{schema.First(logicalKey)}] = @key AND {ActivePredicate(schema)}";
        AddParameters(command, values);
        AddParameter(command, "@key", DbType.Int32, key);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new KeyNotFoundException($"The {schema.Table} record {key} was not found.");
        }
    }

    private async Task DeleteRecordAsync(
        Schema schema,
        string logicalKey,
        string keyName,
        int key,
        int currentUserId,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (schema.Has("is_deleted"))
        {
            var values = new List<WriteValue>();
            AddRequired(
                values,
                schema,
                "is_deleted",
                "is_deleted",
                "@isDeleted",
                DbType.Boolean,
                true
            );
            AddOptional(
                values,
                schema,
                "date_updated",
                "@dateUpdated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptional(
                values,
                schema,
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            command.CommandText =
                $"UPDATE [dbo].[{schema.Table}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [{schema.First(keyName)}] = @key AND {ActivePredicate(schema)}";
            AddParameters(command, values);
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{schema.Table}] WHERE [{schema.First(keyName)}] = @key";
        }

        AddParameter(command, "@key", DbType.Int32, key);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new KeyNotFoundException($"The {schema.Table} record {key} was not found.");
        }
        _ = logicalKey;
    }

    private async Task<ThirdPartyAllocationRecord> GetAllocationOrThrowAsync(
        int allocationId,
        int projectId,
        CancellationToken cancellationToken
    )
    {
        var row = (await GetAllocationsByProjectAsync(projectId, cancellationToken)).FirstOrDefault(
            item => item.allocation_id == allocationId
        );
        return row ?? new ThirdPartyAllocationRecord(allocationId, projectId, null, null, null, 1);
    }

    private async Task<ThirdPartySupplierRecord> GetSupplierOrThrowAsync(
        int supplierId,
        CancellationToken cancellationToken
    ) =>
        await GetSupplierAsync(supplierId, cancellationToken)
        ?? throw new InvalidOperationException(
            $"Supplier {supplierId} could not be read after saving."
        );

    private async Task<ThirdPartyProjectRecord> GetProjectOrThrowAsync(
        int projectId,
        CancellationToken cancellationToken
    ) =>
        await GetProjectAsync(projectId, cancellationToken)
        ?? throw new InvalidOperationException(
            $"Project {projectId} could not be read after saving."
        );

    private async Task<Schema?> GetProjectSchemaOrNullAsync(CancellationToken cancellationToken)
    {
        var schema = await GetSchemaAsync(ProjectTable, cancellationToken);
        return schema is not null && schema.First("project_id", "Project_id") is not null
            ? schema
            : null;
    }

    private async Task<Schema?> GetSchemaAsync(
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = """
            SELECT [TABLE_NAME]
            FROM [INFORMATION_SCHEMA].[TABLES]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);
        var actualTable = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (string.IsNullOrWhiteSpace(actualTable))
        {
            return null;
        }

        await using var columnsCommand = scope.Connection.CreateCommand();
        columnsCommand.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(columnsCommand, "@schema", DbType.String, "dbo");
        AddParameter(columnsCommand, "@table", DbType.String, actualTable);
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var column = reader.GetString(0);
            columns[column] = column;
        }

        return new Schema(actualTable, columns);
    }

    private async Task<Schema?> GetFirstSchemaAsync(
        CancellationToken cancellationToken,
        params string[] tableNames
    )
    {
        foreach (var tableName in tableNames)
        {
            var schema = await GetSchemaAsync(tableName, cancellationToken);
            if (schema is not null)
                return schema;
        }

        return null;
    }

    private async Task<T> InTransactionAsync<T>(
        Func<DbConnection, DbTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var ownsTransaction = transaction is null;
        if (ownsTransaction)
        {
            transaction = await scope.Connection.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await operation(scope.Connection, transaction!, cancellationToken);
            if (ownsTransaction)
                await transaction!.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (ownsTransaction)
                await transaction!.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (ownsTransaction)
                await transaction!.DisposeAsync();
        }
    }

    private async Task InTransactionAsync(
        Func<DbConnection, DbTransaction, CancellationToken, Task> operation,
        CancellationToken cancellationToken
    )
    {
        await InTransactionAsync(
            async (connection, transaction, token) =>
            {
                await operation(connection, transaction, token);
                return true;
            },
            cancellationToken
        );
    }

    private async Task<ConnectionScope> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);
        return new ConnectionScope(connection, shouldClose);
    }

    private static async Task<List<ThirdPartySupplierRecord>> ReadSuppliersAsync(
        DbCommand command,
        CancellationToken cancellationToken
    )
    {
        var results = new List<ThirdPartySupplierRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(
                new ThirdPartySupplierRecord(
                    ReadInt32(reader, "supplier_id") ?? 0,
                    ReadString(reader, "name"),
                    ReadString(reader, "address"),
                    ReadString(reader, "postal_address"),
                    ReadString(reader, "tel"),
                    ReadString(reader, "fax"),
                    ReadString(reader, "cell"),
                    ReadString(reader, "email"),
                    ReadString(reader, "contact_person"),
                    ReadString(reader, "notes"),
                    ReadString(reader, "service_code"),
                    ReadBoolean(reader, "active")
                )
            );
        }

        return results;
    }

    private static async Task<List<ThirdPartyAllocationRecord>> ReadAllocationsAsync(
        DbCommand command,
        CancellationToken cancellationToken
    )
    {
        var results = new List<ThirdPartyAllocationRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(
                new ThirdPartyAllocationRecord(
                    ReadInt32(reader, "allocation_id") ?? 0,
                    ReadInt32(reader, "project_id") ?? 0,
                    ReadInt32(reader, "supplier_id"),
                    ReadInt32(reader, "vehicle_id"),
                    ReadInt32(reader, "class_id"),
                    ReadInt32(reader, "quantity")
                )
            );
        }

        return results;
    }

    private static string ActivePredicate(Schema schema) =>
        schema.Has("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string Projection(
        Schema schema,
        string alias,
        string sqlType,
        params string[] candidates
    )
    {
        var column = schema.First(candidates);
        return column is null
            ? $"CAST(NULL AS {sqlType}) AS [{alias}]"
            : $"[{column}] AS [{alias}]";
    }

    private static string OptionalQualifiedProjection(
        Schema schema,
        string alias,
        string sqlType,
        string candidate,
        string? alternate = null,
        string qualify = ""
    )
    {
        var column = schema.First(candidate, alternate ?? string.Empty);
        if (column is null)
            return $"CAST(NULL AS {sqlType}) AS [{alias}]";
        return string.IsNullOrWhiteSpace(qualify)
            ? $"[{column}] AS [{alias}]"
            : $"[{qualify}].[{column}] AS [{alias}]";
    }

    private static string Column(Schema schema, params string[] candidates) =>
        $"[{schema.First(candidates) ?? throw new InvalidOperationException($"A required compatibility column is missing from {schema.Table}.")}]";

    private static string QualifiedColumn(
        Schema schema,
        string qualifier,
        params string[] candidates
    ) =>
        $"[{qualifier}].[{schema.First(candidates) ?? throw new InvalidOperationException($"A required compatibility column is missing from {schema.Table}.")}]";

    private static string ColumnForRequired(Schema? schema, params string[] candidates) =>
        schema is null
            ? throw new InvalidOperationException(
                "Third-party project storage is not available in this database."
            )
            : Column(schema, candidates);

    private static void AddRequired(
        List<WriteValue> values,
        Schema schema,
        string logicalName,
        string errorName,
        string parameter,
        DbType type,
        object? value,
        params string[] aliases
    )
    {
        var candidates = new[] { logicalName }.Concat(aliases).ToArray();
        var column =
            schema.First(candidates)
            ?? throw new InvalidOperationException(
                $"The required third-party compatibility column {errorName} is not available in {schema.Table}."
            );
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddOptional(
        List<WriteValue> values,
        Schema schema,
        string logicalName,
        string parameter,
        DbType type,
        object? value,
        params string[] aliases
    )
    {
        var candidates = new[] { logicalName }.Concat(aliases).ToArray();
        var column = schema.First(candidates);
        if (
            column is not null
            && !values.Any(value =>
                string.Equals(value.Parameter, parameter, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddAuditCreate(List<WriteValue> values, Schema schema, int currentUserId)
    {
        AddOptional(
            values,
            schema,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptional(
            values,
            schema,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptional(values, schema, "is_deleted", "@isDeleted", DbType.Boolean, false);
    }

    private static void AddAuditUpdate(List<WriteValue> values, Schema schema, int currentUserId)
    {
        AddOptional(
            values,
            schema,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptional(
            values,
            schema,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void ValidateSupplier(ThirdPartySupplierWrite input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.name))
            throw new ArgumentException("Supplier name is required.", nameof(input));
    }

    private static void ValidateProject(ThirdPartyProjectWrite input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.department_code <= 0)
            throw new ArgumentException("Department is required.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.description))
            throw new ArgumentException("Project description is required.", nameof(input));
        if (input.start_date == default || input.end_date == default)
            throw new ArgumentException("Project start and end dates are required.", nameof(input));
        if (input.end_date < input.start_date)
            throw new ArgumentException(
                "Project end date cannot be earlier than its start date.",
                nameof(input)
            );
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.Trim();
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private sealed record Schema(string Table, IReadOnlyDictionary<string, string> Columns)
    {
        public bool Has(params string[] candidates) => First(candidates) is not null;

        public bool HasAll(params string[] candidates) =>
            candidates.All(candidate => First(candidate) is not null);

        public string? First(params string[] candidates) =>
            candidates.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate) && Columns.ContainsKey(candidate)
            );
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            _shouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private readonly bool _shouldClose;

        public async ValueTask DisposeAsync()
        {
            if (_shouldClose)
                await Connection.CloseAsync();
        }
    }
}
