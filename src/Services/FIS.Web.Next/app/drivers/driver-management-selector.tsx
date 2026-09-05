"use client";

import Link from "next/link";
import { useMemo, useState } from "react";

import { contextPath } from "@/app/drivers/access";
import type { DriverManagementDepartment, DriverManagementSite } from "@/lib/api-driver-management";

type DriverManagementSelectorProps = {
  departments: readonly DriverManagementDepartment[];
  sites: readonly DriverManagementSite[];
  initialDepartmentCode: number | null;
  initialSiteCode: number | null;
};

export default function DriverManagementSelector({
  departments,
  sites,
  initialDepartmentCode,
  initialSiteCode,
}: Readonly<DriverManagementSelectorProps>) {
  const [departmentCode, setDepartmentCode] = useState(String(initialDepartmentCode ?? ""));
  const [siteCode, setSiteCode] = useState(String(initialSiteCode ?? ""));
  const availableSites = useMemo(
    () => sites.filter((site) => !departmentCode || site.departmentCode === Number(departmentCode)),
    [departmentCode, sites],
  );
  const selectedDepartment = Number(departmentCode);
  const selectedSite = Number(siteCode);
  const validSelection = Number.isSafeInteger(selectedDepartment) && selectedDepartment > 0 && Number.isSafeInteger(selectedSite) && selectedSite > 0 && availableSites.some((site) => site.code === selectedSite);
  const authorisersPath = validSelection ? contextPath("/drivers/authorisers", selectedDepartment, selectedSite) : "#";
  const driversPath = validSelection ? contextPath("/drivers/site-drivers", selectedDepartment, selectedSite) : "#";

  function changeDepartment(value: string) {
    setDepartmentCode(value);
    const nextDepartmentCode = Number(value);
    const currentSite = sites.find((site) => site.code === Number(siteCode));
    setSiteCode(currentSite?.departmentCode === nextDepartmentCode ? siteCode : "");
  }

  return (
    <section className="vehicle-form-section" aria-labelledby="driver-management-selection-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy workflow</p>
          <h2 id="driver-management-selection-title">Select Department and Site</h2>
          <p>Choose the operating context before opening authoriser or site-driver maintenance.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="driver-management-department">Department</label>
          <select
            className="form-select"
            id="driver-management-department"
            value={departmentCode}
            onChange={(event) => changeDepartment(event.target.value)}
          >
            <option value="">Select department</option>
            {departments.map((department) => (
              <option key={department.code} value={department.code}>{department.description} ({department.code})</option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="driver-management-site">Site</label>
          <select
            className="form-select"
            disabled={!departmentCode}
            id="driver-management-site"
            value={siteCode}
            onChange={(event) => setSiteCode(event.target.value)}
          >
            <option value="">{departmentCode ? "Select site" : "Select department first"}</option>
            {availableSites.map((site) => (
              <option key={site.code} value={site.code}>{site.description} ({site.code})</option>
            ))}
          </select>
        </div>
      </div>
      {validSelection ? (
        <div className="notice notice-info" role="status">
          Selected department {selectedDepartment} / site {selectedSite}.
        </div>
      ) : null}
      <div className="button-row">
        <Link aria-disabled={!validSelection} className={`button button-primary${validSelection ? "" : " button-disabled"}`} href={authorisersPath} onClick={(event) => { if (!validSelection) event.preventDefault(); }}>Authoriser Management</Link>
        <Link aria-disabled={!validSelection} className={`button button-primary${validSelection ? "" : " button-disabled"}`} href={driversPath} onClick={(event) => { if (!validSelection) event.preventDefault(); }}>Site Driver Management</Link>
      </div>
    </section>
  );
}
