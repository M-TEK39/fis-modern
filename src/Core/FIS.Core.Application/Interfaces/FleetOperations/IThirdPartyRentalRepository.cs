namespace FIS.Core.Application.Interfaces;

public interface IThirdPartyRentalRepository
{
    Task<IReadOnlyList<ThirdPartySupplierRecord>> GetSuppliersAsync(
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartySupplierPage> GetSuppliersPageAsync(
        ThirdPartySupplierPageQuery query,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartySupplierRecord?> GetSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartySupplierRecord> CreateSupplierAsync(
        ThirdPartySupplierWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartySupplierRecord> UpdateSupplierAsync(
        int supplierId,
        ThirdPartySupplierWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<ThirdPartyServiceOption>> GetServiceOptionsAsync(
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<ThirdPartyDepartmentOption>?> GetRentalDepartmentsAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ThirdPartyProjectRecord>> GetProjectsAsync(
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyProjectPage> GetProjectsPageAsync(
        ThirdPartyProjectPageQuery query,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<ThirdPartyProjectRecord>> GetProjectsByDepartmentAsync(
        short departmentCode,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyProjectRecord?> GetProjectAsync(
        int projectId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyProjectRecord> CreateProjectAsync(
        ThirdPartyProjectWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyProjectRecord> UpdateProjectAsync(
        int projectId,
        ThirdPartyProjectWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    );
    Task DeleteProjectAsync(
        int projectId,
        int currentUserId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ThirdPartyAllocationRecord>> GetAllocationsByProjectAsync(
        int projectId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyAllocationPage> GetAllocationsByProjectPageAsync(
        ThirdPartyAllocationPageQuery query,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyAllocationRecord> CreateAllocationAsync(
        ThirdPartyAllocationWrite input,
        int currentUserId,
        CancellationToken cancellationToken = default
    );
    Task DeleteAllocationAsync(
        int allocationId,
        int currentUserId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ThirdPartyVehicleRecord>> GetVehiclesBySupplierAsync(
        int supplierId,
        CancellationToken cancellationToken = default
    );
    Task<ThirdPartyVehiclePage> GetVehiclesBySupplierPageAsync(
        ThirdPartyVehiclePageQuery query,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<ThirdPartyClassRequirementRecord>> GetClassRequirementsAsync(
        int projectId,
        CancellationToken cancellationToken = default
    );
}

public sealed record ThirdPartySupplierPageQuery(int Page = 1, int PageSize = 24);

public sealed record ThirdPartyProjectPageQuery(
    int Page = 1,
    int PageSize = 24,
    short? DepartmentCode = null
);

public sealed record ThirdPartyAllocationPageQuery(int ProjectId, int Page = 1, int PageSize = 24);

public sealed record ThirdPartyVehiclePageQuery(int SupplierId, int Page = 1, int PageSize = 24);

public sealed record ThirdPartySupplierPage(
    IReadOnlyList<ThirdPartySupplierRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record ThirdPartyProjectPage(
    IReadOnlyList<ThirdPartyProjectRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record ThirdPartyAllocationPage(
    IReadOnlyList<ThirdPartyAllocationRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record ThirdPartyVehiclePage(
    IReadOnlyList<ThirdPartyVehicleRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record ThirdPartySupplierRecord(
    int supplier_id,
    string? name,
    string? address,
    string? postal_address,
    string? tel,
    string? fax,
    string? cell,
    string? email,
    string? contact_person,
    string? notes,
    string? service_code,
    bool? active
);

public sealed record ThirdPartySupplierWrite(
    string name,
    string? address,
    string? postal_address,
    string? tel,
    string? fax,
    string? cell,
    string? email,
    string? contact_person,
    string? notes,
    string? service_code,
    bool active,
    int ctg_code = 2,
    bool is_third_party = true
);

public sealed record ThirdPartyServiceOption(int code, string name);

public sealed record ThirdPartyDepartmentOption(short department_code, string? description);

public sealed record ThirdPartyProjectRecord(
    int project_id,
    short? department_code,
    short? site_code,
    string? description,
    DateTime? start_date,
    DateTime? end_date,
    string? responsible_person,
    string? rp_physical_address,
    string? rp_postal_address,
    string? rp_tel,
    string? rp_fax,
    string? rp_email,
    string? rp_cell,
    string? notes,
    string? order_reference,
    string? class_configuration
);

public sealed record ThirdPartyProjectWrite(
    short department_code,
    short? site_code,
    string description,
    DateTime start_date,
    DateTime end_date,
    string? responsible_person,
    string? rp_physical_address,
    string? rp_postal_address,
    string? rp_tel,
    string? rp_fax,
    string? rp_email,
    string? rp_cell,
    string? notes,
    string? order_reference,
    string? class_configuration
);

public sealed record ThirdPartyAllocationRecord(
    int allocation_id,
    int project_id,
    int? supplier_id,
    int? vehicle_id,
    int? class_id,
    int? quantity
);

public sealed record ThirdPartyAllocationWrite(
    int project_id,
    int? supplier_id,
    int? vehicle_id,
    int? class_id,
    int? quantity
);

public sealed record ThirdPartyVehicleRecord(
    int vehicle_id,
    string? registration_number,
    string? model_description,
    string? model_year,
    string? chassis_number
);

public sealed record ThirdPartyClassRequirementRecord(
    int class_id,
    string? class_name,
    int? required_count
);
