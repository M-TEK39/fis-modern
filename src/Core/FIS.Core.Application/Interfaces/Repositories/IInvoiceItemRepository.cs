using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces.Repositories;

/// <summary>
/// Repository interface for InvoiceItem entity operations.
/// Handles invoice line item persistence and retrieval operations.
/// </summary>
public interface IInvoiceItemRepository
{
    /// <summary>
    /// Get an invoice item by its ID.
    /// </summary>
    /// <param name="id">Invoice item identifier</param>
    /// <returns>Invoice item if found</returns>
    Task<InvoiceItem?> GetByIdAsync(int id);

    /// <summary>
    /// Get all invoice items.
    /// </summary>
    /// <returns>List of all invoice items</returns>
    Task<List<InvoiceItem>> GetAllAsync();

    /// <summary>
    /// Add a new invoice item.
    /// </summary>
    /// <param name="invoiceItem">Invoice item to add</param>
    /// <returns>Added invoice item</returns>
    Task<InvoiceItem> AddAsync(InvoiceItem invoiceItem);

    /// <summary>
    /// Update an existing invoice item.
    /// </summary>
    /// <param name="invoiceItem">Invoice item to update</param>
    /// <returns>Updated invoice item</returns>
    Task<InvoiceItem> UpdateAsync(InvoiceItem invoiceItem, int currentUserId);

    /// <summary>
    /// Delete an invoice item by ID.
    /// </summary>
    /// <param name="id">Invoice item identifier</param>
    /// <returns>True if deleted</returns>
    Task<bool> DeleteAsync(int id, int currentUserId);

    /// <summary>
    /// Get all items for a specific invoice.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <returns>List of invoice items</returns>
    Task<List<InvoiceItem>> GetByInvoiceIdAsync(int invoiceCode);

    /// <summary>
    /// Get invoice items by contract type and date range.
    /// </summary>
    /// <param name="contractType">Contract type</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items for the contract type in the period</returns>
    Task<List<InvoiceItem>> GetByContractTypeAndPeriodAsync(
        string contractType,
        DateTime fromDate,
        DateTime toDate);

    /// <summary>
    /// Get invoice items by vehicle and date range.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items for the vehicle in the period</returns>
    Task<List<InvoiceItem>> GetByVehicleAndPeriodAsync(
        int vmfCode,
        DateTime fromDate,
        DateTime toDate);

    /// <summary>
    /// Get invoice items by contract type.
    /// </summary>
    /// <param name="contractType">Contract type</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items of the specified contract type</returns>
    Task<List<InvoiceItem>> GetByTypeAndPeriodAsync(
        string contractType,
        DateTime fromDate,
        DateTime toDate);

    /// <summary>
    /// Delete all items for an invoice (used for cancellations).
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <returns>Number of items deleted</returns>
    Task<int> DeleteByInvoiceIdAsync(int invoiceCode);

    /// <summary>
    /// Get summary totals for invoice items by department.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>Summary totals</returns>
    Task<InvoiceItemSummary> GetSummaryByDepartmentAsync(
        int departmentCode,
        DateTime fromDate,
        DateTime toDate);
}

/// <summary>
/// Summary totals for invoice items.
/// </summary>
public class InvoiceItemSummary
{
    public int DepartmentCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalFixedAmount { get; set; }
    public decimal TotalKilometerAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public int ContractCount { get; set; }
}