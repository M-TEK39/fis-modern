using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for Invoice entity operations.
/// Handles invoice persistence and retrieval operations.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// Get an invoice by its ID.
    /// </summary>
    /// <param name="id">Invoice identifier</param>
    /// <returns>Invoice if found</returns>
    Task<Invoice?> GetByIdAsync(int id);

    /// <summary>
    /// Get all invoices.
    /// </summary>
    /// <returns>List of all invoices</returns>
    Task<List<Invoice>> GetAllAsync();

    /// <summary>
    /// Add a new invoice.
    /// </summary>
    /// <param name="invoice">Invoice to add</param>
    /// <returns>Added invoice</returns>
    Task<Invoice> AddAsync(Invoice invoice);

    /// <summary>
    /// Update an existing invoice.
    /// </summary>
    /// <param name="invoice">Invoice to update</param>
    /// <returns>Updated invoice</returns>
    Task<Invoice> UpdateAsync(Invoice invoice, int currentUserId);

    /// <summary>
    /// Delete an invoice by ID.
    /// </summary>
    /// <param name="id">Invoice identifier</param>
    /// <returns>True if deleted</returns>
    Task<bool> DeleteAsync(int id, int currentUserId);

    /// <summary>
    /// Get invoices by department and date range.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoices in the period</returns>
    Task<List<Invoice>> GetByDepartmentAndPeriodAsync(
        int departmentCode,
        DateTime fromDate,
        DateTime toDate
    );

    /// <summary>
    /// Get invoices by department.
    /// </summary>
    /// <param name="departmentCode">Department code</param>
    /// <returns>List of invoices for the department</returns>
    Task<List<Invoice>> GetByDepartmentAsync(short departmentCode);

    /// <summary>
    /// Get invoices by posting month.
    /// </summary>
    /// <param name="postingMonthCode">Posting month code (YYYYMM format)</param>
    /// <returns>List of invoices for the month</returns>
    Task<List<Invoice>> GetByPostingMonthAsync(short postingMonthCode);

    /// <summary>
    /// Get all invoices for a specific posting month range.
    /// </summary>
    /// <param name="fromMonthCode">Start month code (YYYYMM)</param>
    /// <param name="toMonthCode">End month code (YYYYMM)</param>
    /// <returns>List of invoices in the month range</returns>
    Task<List<Invoice>> GetByMonthRangeAsync(short fromMonthCode, short toMonthCode);

    /// <summary>
    /// Get invoice by code.
    /// </summary>
    /// <param name="invoiceCode">Invoice code</param>
    /// <returns>Invoice if found</returns>
    Task<Invoice?> GetByCodeAsync(int invoiceCode);

    /// <summary>
    /// Update invoice department.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="departmentCode">New department code</param>
    /// <returns>Updated invoice</returns>
    Task<Invoice> UpdateDepartmentAsync(int invoiceCode, short departmentCode);
}
