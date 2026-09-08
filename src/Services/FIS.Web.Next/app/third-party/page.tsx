import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  createThirdPartyAllocationAction,
  deleteThirdPartyAllocationAction,
  saveThirdPartyProjectAction,
  saveThirdPartySupplierAction,
} from "@/app/third-party/actions";
import {
  getThirdPartyAllocations,
  getThirdPartyDepartments,
  getThirdPartyProjects,
  getThirdPartyRequirements,
  getThirdPartyServices,
  getThirdPartySites,
  getThirdPartySuppliers,
  getThirdPartyVehicles,
  ThirdPartyApiError,
  type ThirdPartyDepartment,
  type ThirdPartyProject,
  type ThirdPartySupplier,
} from "@/lib/api-third-party";
import { getSession } from "@/lib/session";

const CONTRACT_MANAGEMENT_PERMISSION = BigInt(2);
const THIRD_PARTY_ROLE = "Third Party Rental";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type Tab = "suppliers" | "projects" | "allocation";

export type ThirdPartyPageProps = { searchParams: SearchParams; routePath?: string };

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positive(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function selectedTab(value: string | undefined): Tab {
  return value === "projects" || value === "allocation" ? value : "suppliers";
}

function hasThirdPartyAccess(accessLevel: string | undefined, roles: readonly string[]) {
  const hasRole = roles.some(
    (role) => role.localeCompare(THIRD_PARTY_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
  if (hasRole) return true;
  if (!accessLevel) return false;
  try {
    return (
      (BigInt(accessLevel) & CONTRACT_MANAGEMENT_PERMISSION) === CONTRACT_MANAGEMENT_PERMISSION
    );
  } catch {
    return false;
  }
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function dateValue(value: string | null) {
  return value?.slice(0, 10) ?? "";
}

function href(path: string, values: Record<string, string | number | null | undefined>) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(values))
    if (value !== null && value !== undefined && String(value) !== "")
      query.set(key, String(value));
  const encoded = query.toString();
  return encoded ? `${path}?${encoded}` : path;
}

function notice(query: Record<string, string | string[] | undefined>) {
  const saved = first(query.saved);
  if (saved === "created")
    return { tone: "success", text: "Record created successfully." } as const;
  if (saved === "updated")
    return { tone: "success", text: "Record updated successfully." } as const;
  if (saved === "allocated")
    return { tone: "success", text: "Vehicle allocated successfully." } as const;
  if (first(query.deleted) === "1")
    return { tone: "success", text: "Allocation removed successfully." } as const;
  const error = first(query.error);
  return error ? ({ tone: "error", text: error } as const) : null;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain third-party rentals.</h2>
      <p className="muted-copy">
        Your account needs contract-management or third-party rental access for this workflow.
      </p>
      <Link className="button button-secondary" href="/home">
        Home
      </Link>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Third-party rental data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

function SupplierForm({
  supplier,
  services,
  mode,
}: Readonly<{
  supplier: ThirdPartySupplier | null;
  services: { code: number; name: string }[];
  mode: "add" | "edit";
}>) {
  return (
    <form action={saveThirdPartySupplierAction} className="vehicle-status-maintenance-panel">
      <input name="supplierId" type="hidden" value={supplier?.supplierId ?? ""} readOnly />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{mode === "add" ? "New supplier" : "Supplier details"}</p>
          <h2>{mode === "add" ? "Add New Supplier" : "Edit Supplier"}</h2>
          <p>All original supplier contact and service fields remain available.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-name">
            Supplier Name <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="third-party-supplier-name"
            name="name"
            defaultValue={supplier?.name ?? ""}
            maxLength={200}
            required
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-supplier-address">
            Physical Address
          </label>
          <textarea
            className="form-input"
            id="third-party-supplier-address"
            name="address"
            defaultValue={supplier?.address ?? ""}
            rows={2}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-supplier-postal">
            Postal Address
          </label>
          <textarea
            className="form-input"
            id="third-party-supplier-postal"
            name="postalAddress"
            defaultValue={supplier?.postalAddress ?? ""}
            rows={2}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-tel">
            Office Number
          </label>
          <input
            className="form-input"
            id="third-party-supplier-tel"
            name="tel"
            defaultValue={supplier?.tel ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-cell">
            Cell Number
          </label>
          <input
            className="form-input"
            id="third-party-supplier-cell"
            name="cell"
            defaultValue={supplier?.cell ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-fax">
            Fax Number
          </label>
          <input
            className="form-input"
            id="third-party-supplier-fax"
            name="fax"
            defaultValue={supplier?.fax ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-email">
            E-mail Address
          </label>
          <input
            className="form-input"
            id="third-party-supplier-email"
            name="email"
            type="email"
            defaultValue={supplier?.email ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-contact">
            Contact Person
          </label>
          <input
            className="form-input"
            id="third-party-supplier-contact"
            name="contactPerson"
            defaultValue={supplier?.contactPerson ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-service">
            Service
          </label>
          <select
            className="form-select"
            id="third-party-supplier-service"
            name="serviceCode"
            defaultValue={supplier?.serviceCode ?? ""}
          >
            <option value="">Select...</option>
            {services.map((service) => (
              <option key={service.code} value={service.code}>
                {service.name}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-supplier-active">
            Status
          </label>
          <select
            className="form-select"
            id="third-party-supplier-active"
            name="active"
            defaultValue={supplier?.active === false ? "0" : "1"}
          >
            <option value="1">Active</option>
            <option value="0">Inactive</option>
          </select>
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-supplier-notes">
            Notes
          </label>
          <textarea
            className="form-input"
            id="third-party-supplier-notes"
            name="notes"
            defaultValue={supplier?.notes ?? ""}
            rows={3}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {mode === "add" ? "Submit" : "Save Changes"}
        </button>
        <Link className="button button-secondary" href="/third-party">
          Cancel
        </Link>
      </div>
    </form>
  );
}

function SupplierTab({
  suppliers,
  services,
  supplier,
  supplierId,
  mode,
}: Readonly<{
  suppliers: ThirdPartySupplier[];
  services: { code: number; name: string }[];
  supplier: ThirdPartySupplier | null;
  supplierId: number | null;
  mode: string | undefined;
}>) {
  const editMode = mode === "edit";
  return (
    <>
      <div className="button-row">
        <Link
          className="button button-secondary"
          href={href("/third-party", { tab: "suppliers", mode: "add" })}
        >
          Add New Supplier
        </Link>
        <Link
          className="button button-secondary"
          href={href("/third-party", { tab: "suppliers", mode: "edit" })}
        >
          Edit Supplier
        </Link>
      </div>
      {editMode ? (
        <>
          <form className="vehicle-create-form" method="get">
            <input name="tab" type="hidden" value="suppliers" />
            <input name="mode" type="hidden" value="edit" />
            <div className="form-field">
              <label className="form-label" htmlFor="third-party-select-supplier">
                Select a Supplier
              </label>
              <select
                className="form-select"
                id="third-party-select-supplier"
                name="supplierId"
                defaultValue={supplierId ?? ""}
              >
                <option value="">Select...</option>
                {suppliers.map((item) => (
                  <option key={item.supplierId} value={item.supplierId}>
                    {valueOrDash(item.name)} ({item.supplierId})
                  </option>
                ))}
              </select>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Load Supplier
              </button>
            </div>
          </form>
          {supplier ? (
            <SupplierForm mode="edit" services={services} supplier={supplier} />
          ) : (
            <div className="vehicle-empty-state">
              <p>Select a supplier to edit.</p>
            </div>
          )}
        </>
      ) : mode === "add" ? (
        <SupplierForm mode="add" services={services} supplier={null} />
      ) : (
        <div className="vehicle-empty-state">
          <p>Welcome to the supplier information screen. Select a task above.</p>
        </div>
      )}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Supplier register</p>
          <h2>
            {suppliers.length} supplier{suppliers.length === 1 ? "" : "s"}
          </h2>
        </div>
      </div>
      {suppliers.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No third-party suppliers were found in the available schema.</p>
        </div>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Third-party supplier register</caption>
            <thead>
              <tr>
                <th scope="col">Supplier</th>
                <th scope="col">Service</th>
                <th scope="col">Contact</th>
                <th scope="col">Telephone</th>
                <th scope="col">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {suppliers.map((item) => (
                <tr key={item.supplierId}>
                  <td>{valueOrDash(item.name)}</td>
                  <td>{valueOrDash(item.serviceCode)}</td>
                  <td>{valueOrDash(item.contactPerson)}</td>
                  <td>{valueOrDash(item.tel ?? item.cell)}</td>
                  <td>{item.active === false ? "Inactive" : "Active"}</td>
                  <td>
                    <Link
                      className="button button-secondary button-small"
                      href={href("/third-party", {
                        tab: "suppliers",
                        mode: "edit",
                        supplierId: item.supplierId,
                      })}
                    >
                      Edit
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

function ProjectForm({
  project,
  departments,
  sites,
  mode,
  defaultDepartmentCode,
}: Readonly<{
  project: ThirdPartyProject | null;
  departments: ThirdPartyDepartment[];
  sites: { siteCode: number; description: string | null }[];
  mode: "add" | "edit";
  defaultDepartmentCode?: number | null;
}>) {
  return (
    <form action={saveThirdPartyProjectAction} className="vehicle-status-maintenance-panel">
      <input name="projectId" type="hidden" value={project?.projectId ?? ""} readOnly />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{mode === "add" ? "New project" : "Project details"}</p>
          <h2>{mode === "add" ? "Add New Project" : "Edit Project"}</h2>
          <p>Project contacts, dates, order reference, and class configuration are retained.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-department">
            Department <span className="required">*</span>
          </label>
          <select
            className="form-select"
            id="third-party-project-department"
            name="departmentCode"
            defaultValue={project?.departmentCode ?? defaultDepartmentCode ?? ""}
            required
          >
            <option value="">Select...</option>
            {departments.map((department) => (
              <option key={department.departmentCode} value={department.departmentCode}>
                {valueOrDash(department.description)} ({department.departmentCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-site">
            Site
          </label>
          <select
            className="form-select"
            id="third-party-project-site"
            name="siteCode"
            defaultValue={project?.siteCode ?? ""}
          >
            <option value="">Select...</option>
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {valueOrDash(site.description)} ({site.siteCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-project-description">
            Project Description <span className="required">*</span>
          </label>
          <textarea
            className="form-input"
            id="third-party-project-description"
            name="description"
            defaultValue={project?.description ?? ""}
            rows={3}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-start">
            Project Start Date <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="third-party-project-start"
            name="startDate"
            type="date"
            defaultValue={dateValue(project?.startDate ?? null)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-end">
            Project End Date <span className="required">*</span>
          </label>
          <input
            className="form-input"
            id="third-party-project-end"
            name="endDate"
            type="date"
            defaultValue={dateValue(project?.endDate ?? null)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-responsible">
            Responsible Person
          </label>
          <input
            className="form-input"
            id="third-party-project-responsible"
            name="responsiblePerson"
            defaultValue={project?.responsiblePerson ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-tel">
            Tel Number
          </label>
          <input
            className="form-input"
            id="third-party-project-tel"
            name="responsibleTel"
            defaultValue={project?.responsibleTel ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-cell">
            Cell Number
          </label>
          <input
            className="form-input"
            id="third-party-project-cell"
            name="responsibleCell"
            defaultValue={project?.responsibleCell ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-fax">
            Fax Number
          </label>
          <input
            className="form-input"
            id="third-party-project-fax"
            name="responsibleFax"
            defaultValue={project?.responsibleFax ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-email">
            E-mail Address
          </label>
          <input
            className="form-input"
            id="third-party-project-email"
            name="responsibleEmail"
            type="email"
            defaultValue={project?.responsibleEmail ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-project-address">
            Responsible Person Physical Address
          </label>
          <input
            className="form-input"
            id="third-party-project-address"
            name="responsibleAddress"
            defaultValue={project?.responsibleAddress ?? ""}
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-project-postal">
            Responsible Person Postal Address
          </label>
          <input
            className="form-input"
            id="third-party-project-postal"
            name="responsiblePostalAddress"
            defaultValue={project?.responsiblePostalAddress ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-order">
            Client Order Reference
          </label>
          <input
            className="form-input"
            id="third-party-project-order"
            name="orderReference"
            defaultValue={project?.orderReference ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="third-party-project-classes">
            Class Configuration
          </label>
          <input
            className="form-input"
            id="third-party-project-classes"
            name="classConfiguration"
            defaultValue={project?.classConfiguration ?? ""}
            placeholder="class_id=count, ..."
          />
        </div>
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="third-party-project-notes">
            Project Notes
          </label>
          <textarea
            className="form-input"
            id="third-party-project-notes"
            name="notes"
            defaultValue={project?.notes ?? ""}
            rows={3}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {mode === "add" ? "Submit" : "Save Changes"}
        </button>
        <Link className="button button-secondary" href="/third-party">
          Cancel
        </Link>
      </div>
    </form>
  );
}

function ProjectTab({
  projects,
  departments,
  project,
  projectId,
  mode,
  sites,
  departmentCode,
}: Readonly<{
  projects: ThirdPartyProject[];
  departments: ThirdPartyDepartment[];
  project: ThirdPartyProject | null;
  projectId: number | null;
  mode: string | undefined;
  sites: { siteCode: number; description: string | null }[];
  departmentCode: number | null;
}>) {
  const editMode = mode === "edit";
  return (
    <>
      <div className="button-row">
        <Link
          className="button button-secondary"
          href={href("/third-party", { tab: "projects", mode: "add" })}
        >
          Add New Project
        </Link>
        <Link
          className="button button-secondary"
          href={href("/third-party", { tab: "projects", mode: "edit" })}
        >
          Edit Project
        </Link>
      </div>
      {editMode ? (
        <>
          <form className="vehicle-create-form" method="get">
            <input name="tab" type="hidden" value="projects" />
            <input name="mode" type="hidden" value="edit" />
            <div className="form-field">
              <label className="form-label" htmlFor="third-party-select-project">
                Select a Project
              </label>
              <select
                className="form-select"
                id="third-party-select-project"
                name="projectId"
                defaultValue={projectId ?? ""}
              >
                <option value="">Select...</option>
                {projects.map((item) => (
                  <option key={item.projectId} value={item.projectId}>
                    {valueOrDash(item.description)} ({item.projectId})
                  </option>
                ))}
              </select>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                Load Project
              </button>
            </div>
          </form>
          {project ? (
            <ProjectForm mode="edit" departments={departments} project={project} sites={sites} />
          ) : (
            <div className="vehicle-empty-state">
              <p>Select a project to edit.</p>
            </div>
          )}
        </>
      ) : mode === "add" ? (
        <>
          <form className="vehicle-create-form" method="get">
            <input name="tab" type="hidden" value="projects" />
            <input name="mode" type="hidden" value="add" />
            <div className="form-field">
              <label className="form-label" htmlFor="third-party-add-project-department">
                Load sites for department
              </label>
              <select
                className="form-select"
                id="third-party-add-project-department"
                name="departmentCode"
                defaultValue={departmentCode ?? ""}
              >
                <option value="">Select...</option>
                {departments.map((department) => (
                  <option key={department.departmentCode} value={department.departmentCode}>
                    {valueOrDash(department.description)} ({department.departmentCode})
                  </option>
                ))}
              </select>
            </div>
            <div className="button-row">
              <button className="button button-secondary" type="submit">
                Load Department Sites
              </button>
            </div>
          </form>
          <ProjectForm
            mode="add"
            defaultDepartmentCode={departmentCode}
            departments={departments}
            project={null}
            sites={sites}
          />
        </>
      ) : (
        <div className="vehicle-empty-state">
          <p>Welcome to the project information screen. Select a task above.</p>
        </div>
      )}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Project register</p>
          <h2>
            {projects.length} project{projects.length === 1 ? "" : "s"}
          </h2>
        </div>
      </div>
      {projects.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No third-party projects were found in the available schema.</p>
        </div>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Third-party project register</caption>
            <thead>
              <tr>
                <th scope="col">Description</th>
                <th scope="col">Department</th>
                <th scope="col">Start</th>
                <th scope="col">End</th>
                <th scope="col">Responsible Person</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {projects.map((item) => (
                <tr key={item.projectId}>
                  <td>{valueOrDash(item.description)}</td>
                  <td>{valueOrDash(item.departmentCode)}</td>
                  <td>{dateValue(item.startDate)}</td>
                  <td>{dateValue(item.endDate)}</td>
                  <td>{valueOrDash(item.responsiblePerson)}</td>
                  <td>
                    <Link
                      className="button button-secondary button-small"
                      href={href("/third-party", {
                        tab: "projects",
                        mode: "edit",
                        projectId: item.projectId,
                      })}
                    >
                      Edit
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

function AllocationTab({
  departments,
  suppliers,
  projects,
  selectedDepartment,
  selectedProject,
  selectedSupplier,
  vehicles,
  requirements,
  allocations,
}: Readonly<{
  departments: ThirdPartyDepartment[];
  suppliers: ThirdPartySupplier[];
  projects: ThirdPartyProject[];
  selectedDepartment: number | null;
  selectedProject: number | null;
  selectedSupplier: number | null;
  vehicles: Awaited<ReturnType<typeof getThirdPartyVehicles>>;
  requirements: Awaited<ReturnType<typeof getThirdPartyRequirements>>;
  allocations: Awaited<ReturnType<typeof getThirdPartyAllocations>>;
}>) {
  return (
    <>
      <form className="vehicle-create-form" method="get">
        <input name="tab" type="hidden" value="allocation" />
        <div className="vehicle-create-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="third-party-allocation-department">
              Department
            </label>
            <select
              className="form-select"
              id="third-party-allocation-department"
              name="departmentCode"
              defaultValue={selectedDepartment ?? ""}
            >
              <option value="">Select...</option>
              {departments.map((department) => (
                <option key={department.departmentCode} value={department.departmentCode}>
                  {valueOrDash(department.description)} ({department.departmentCode})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="third-party-allocation-project">
              Project
            </label>
            <select
              className="form-select"
              id="third-party-allocation-project"
              name="projectId"
              defaultValue={selectedProject ?? ""}
            >
              <option value="">Select...</option>
              {projects.map((project) => (
                <option key={project.projectId} value={project.projectId}>
                  {valueOrDash(project.description)} ({project.projectId})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="third-party-allocation-supplier">
              Supplier
            </label>
            <select
              className="form-select"
              id="third-party-allocation-supplier"
              name="supplierId"
              defaultValue={selectedSupplier ?? ""}
            >
              <option value="">Select...</option>
              {suppliers
                .filter((supplier) => supplier.active !== false)
                .map((supplier) => (
                  <option key={supplier.supplierId} value={supplier.supplierId}>
                    {valueOrDash(supplier.name)} ({supplier.supplierId})
                  </option>
                ))}
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Load Allocation Options
          </button>
          <Link
            className="button button-secondary"
            href={href("/third-party", { tab: "allocation" })}
          >
            Clear
          </Link>
        </div>
      </form>
      {!selectedProject || !selectedSupplier ? (
        <div className="vehicle-empty-state">
          <p>Select a department, project, and supplier to load vehicles and class categories.</p>
        </div>
      ) : (
        <>
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Supplier vehicle allocation</p>
              <h2>
                {vehicles.length} available vehicle{vehicles.length === 1 ? "" : "s"}
              </h2>
              <p>Choose a class category for each vehicle before allocating it to the project.</p>
            </div>
          </div>
          {requirements.length === 0 ? (
            <div className="notice notice-warning" role="status">
              No class categories were found for this project. Add a class configuration before
              allocating a vehicle.
            </div>
          ) : vehicles.length === 0 ? (
            <div className="vehicle-empty-state">
              <p>
                No vehicles were found for this supplier in the available legacy or modern vehicle
                source.
              </p>
            </div>
          ) : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Supplier vehicle allocation</caption>
                <thead>
                  <tr>
                    <th scope="col">Vehicle ID</th>
                    <th scope="col">Registration</th>
                    <th scope="col">Model</th>
                    <th scope="col">Year</th>
                    <th scope="col">Class and action</th>
                  </tr>
                </thead>
                <tbody>
                  {vehicles.map((vehicle) => (
                    <tr key={vehicle.vehicleId}>
                      <td>{vehicle.vehicleId}</td>
                      <td>{valueOrDash(vehicle.registrationNumber)}</td>
                      <td>{valueOrDash(vehicle.modelDescription)}</td>
                      <td>{valueOrDash(vehicle.modelYear)}</td>
                      <td>
                        <form action={createThirdPartyAllocationAction} className="button-row">
                          <input
                            name="departmentCode"
                            type="hidden"
                            value={selectedDepartment ?? ""}
                            readOnly
                          />
                          <input name="projectId" type="hidden" value={selectedProject} readOnly />
                          <input
                            name="supplierId"
                            type="hidden"
                            value={selectedSupplier}
                            readOnly
                          />
                          <input
                            name="vehicleId"
                            type="hidden"
                            value={vehicle.vehicleId}
                            readOnly
                          />
                          <input name="quantity" type="hidden" value="1" readOnly />
                          <select
                            className="form-select"
                            name="classId"
                            aria-label={`Class for vehicle ${vehicle.vehicleId}`}
                            defaultValue=""
                            required
                          >
                            <option value="">Select...</option>
                            {requirements.map((requirement) => (
                              <option key={requirement.classId} value={requirement.classId}>
                                {valueOrDash(requirement.className)}
                                {requirement.requiredCount === null
                                  ? ""
                                  : ` (${requirement.requiredCount} required)`}
                              </option>
                            ))}
                          </select>
                          <button className="button button-primary button-small" type="submit">
                            Allocate
                          </button>
                        </form>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Current allocations</p>
              <h2>
                {allocations.length} allocation{allocations.length === 1 ? "" : "s"}
              </h2>
            </div>
          </div>
          {allocations.length === 0 ? (
            <div className="vehicle-empty-state">
              <p>No vehicles are currently allocated to this project.</p>
            </div>
          ) : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Current project allocations</caption>
                <thead>
                  <tr>
                    <th scope="col">Allocation</th>
                    <th scope="col">Supplier</th>
                    <th scope="col">Vehicle</th>
                    <th scope="col">Class</th>
                    <th scope="col">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {allocations.map((allocation) => (
                    <tr key={allocation.allocationId}>
                      <td>{allocation.allocationId}</td>
                      <td>{valueOrDash(allocation.supplierId)}</td>
                      <td>{valueOrDash(allocation.vehicleId)}</td>
                      <td>{valueOrDash(allocation.classId)}</td>
                      <td>
                        <form action={deleteThirdPartyAllocationAction}>
                          <input
                            name="allocationId"
                            type="hidden"
                            value={allocation.allocationId}
                            readOnly
                          />
                          <input name="projectId" type="hidden" value={selectedProject} readOnly />
                          <button className="button button-danger button-small" type="submit">
                            Remove
                          </button>
                        </form>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </>
  );
}

export default async function ThirdPartyPage({
  searchParams,
  routePath = "/third-party",
}: ThirdPartyPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasThirdPartyAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const tab = selectedTab(first(query.tab));
  const mode = first(query.mode);
  const supplierId = positive(first(query.supplierId));
  const projectId = positive(first(query.projectId));
  const departmentCode = positive(first(query.departmentCode));
  const supplierPromise = getThirdPartySuppliers();
  const departmentsPromise = getThirdPartyDepartments();
  const servicesPromise = getThirdPartyServices();
  const projectsPromise = getThirdPartyProjects(departmentCode ?? undefined);

  try {
    const [suppliers, departments, services, projects] = await Promise.all([
      supplierPromise,
      departmentsPromise,
      servicesPromise,
      projectsPromise,
    ]);
    const selectedSupplier =
      supplierId === null
        ? null
        : (suppliers.find((supplier) => supplier.supplierId === supplierId) ?? null);
    const selectedProject =
      projectId === null
        ? null
        : (projects.find((project) => project.projectId === projectId) ?? null);
    const selectedProjectDepartment = selectedProject?.departmentCode ?? departmentCode;
    const sites =
      selectedProjectDepartment === null || selectedProjectDepartment === undefined
        ? []
        : await getThirdPartySites(selectedProjectDepartment);
    const allocationProjects =
      tab === "allocation" && departmentCode === null ? projects : projects;
    const vehicles =
      tab === "allocation" && supplierId !== null ? await getThirdPartyVehicles(supplierId) : [];
    const requirements =
      tab === "allocation" && projectId !== null ? await getThirdPartyRequirements(projectId) : [];
    const allocations =
      tab === "allocation" && projectId !== null ? await getThirdPartyAllocations(projectId) : [];
    const message = notice(query);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="third-party-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Contract Management</p>
              <h1 id="third-party-title">Third Party Rental Suppliers</h1>
              <p>Supplier, project, and allocation maintenance.</p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/home">
                Home
              </Link>
              <form action={logoutAction}>
                <button className="button button-secondary" type="submit">
                  Sign out
                </button>
              </form>
            </div>
          </header>
          {message ? (
            <div
              className={`notice notice-${message.tone}`}
              role={message.tone === "error" ? "alert" : "status"}
            >
              {message.text}
            </div>
          ) : null}
          <nav className="tabs" aria-label="Third-party rental sections">
            <Link
              className={`tab ${tab === "suppliers" ? "active" : ""}`}
              href={href(routePath, { tab: "suppliers" })}
            >
              Suppliers
            </Link>
            <Link
              className={`tab ${tab === "projects" ? "active" : ""}`}
              href={href(routePath, { tab: "projects" })}
            >
              Project
            </Link>
            <Link
              className={`tab ${tab === "allocation" ? "active" : ""}`}
              href={href(routePath, { tab: "allocation" })}
            >
              Supplier &amp; Vehicle Allocation
            </Link>
          </nav>
          {tab === "suppliers" ? (
            <SupplierTab
              mode={mode}
              services={services}
              supplier={selectedSupplier}
              supplierId={supplierId}
              suppliers={suppliers}
            />
          ) : tab === "projects" ? (
            <ProjectTab
              departmentCode={departmentCode}
              departments={departments}
              mode={mode}
              project={selectedProject}
              projectId={projectId}
              projects={projects}
              sites={sites}
            />
          ) : (
            <AllocationTab
              allocations={allocations}
              departments={departments}
              projects={allocationProjects}
              requirements={requirements}
              selectedDepartment={departmentCode}
              selectedProject={projectId}
              selectedSupplier={supplierId}
              suppliers={suppliers}
              vehicles={vehicles}
            />
          )}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ThirdPartyApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS third-party rental request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}
