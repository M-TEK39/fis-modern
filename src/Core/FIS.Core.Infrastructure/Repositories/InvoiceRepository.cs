using FIS.Core.Application.Interfaces.Repositories;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Invoice entity operations.
/// Handles invoice persistence and retrieval operations.
/// </summary>
public class InvoiceRepository : IInvoiceRepository
{
    private readonly FisDbContext _context;

    public InvoiceRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get an invoice by its ID.
    /// </summary>
    /// <param name="id">Invoice identifier</param>
    /// <returns>Invoice if found</returns>
    public async Task<Invoice?> GetByIdAsync(int id)
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.invoice_code == id);
    }

    /// <summary>
    /// Get all invoices.
    /// </summary>
    /// <returns>List of all invoices</returns>
    public async Task<List<Invoice>> GetAllAsync()
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .OrderByDescending(i => i.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Add a new invoice.
    /// </summary>
    /// <param name="invoice">Invoice to add</param>
    /// <returns>Added invoice</returns>
    public async Task<Invoice> AddAsync(Invoice invoice)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    /// <summary>
    /// Update an existing invoice.
    /// </summary>
    /// <param name="invoice">Invoice to update</param>
    /// <returns>Updated invoice</returns>
    public async Task<Invoice> UpdateAsync(Invoice invoice)
    {
        if (invoice == null)
            throw new ArgumentNullException(nameof(invoice));

        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    /// <summary>
    /// Delete an invoice by ID.
    /// </summary>
    /// <param name="id">Invoice identifier</param>
    /// <returns>True if deleted</returns>
    public async Task<bool> DeleteAsync(int id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null)
            return false;

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get invoices by department and date range.
    /// Note: Since invoice table only stores posting_month_code, this filters by month/year.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="fromDate">Start date (month/year will be used)</param>
    /// <param name="toDate">End date (month/year will be used)</param>
    /// <returns>List of invoices in the period</returns>
    public async Task<List<Invoice>> GetByDepartmentAndPeriodAsync(
        int departmentCode,
        DateTime fromDate,
        DateTime toDate)
    {
        var fromMonthCode = (short)(fromDate.Year * 100 + fromDate.Month);
        var toMonthCode = (short)(toDate.Year * 100 + toDate.Month);
        
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Where(i => i.department_code == departmentCode && 
                       i.posting_month_code >= fromMonthCode && 
                       i.posting_month_code <= toMonthCode)
            .OrderByDescending(i => i.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoices by department.
    /// </summary>
    /// <param name="departmentCode">Department code</param>
    /// <returns>List of invoices for the department</returns>
    public async Task<List<Invoice>> GetByDepartmentAsync(short departmentCode)
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Where(i => i.department_code == departmentCode)
            .OrderByDescending(i => i.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoices by posting month.
    /// </summary>
    /// <param name="postingMonthCode">Posting month code (YYYYMM format)</param>
    /// <returns>List of invoices for the month</returns>
    public async Task<List<Invoice>> GetByPostingMonthAsync(short postingMonthCode)
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Where(i => i.posting_month_code == postingMonthCode)
            .ToListAsync();
    }

    /// <summary>
    /// Get all invoices for a specific posting month range.
    /// </summary>
    /// <param name="fromMonthCode">Start month code (YYYYMM)</param>
    /// <param name="toMonthCode">End month code (YYYYMM)</param>
    /// <returns>List of invoices in the month range</returns>
    public async Task<List<Invoice>> GetByMonthRangeAsync(short fromMonthCode, short toMonthCode)
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .Where(i => i.posting_month_code >= fromMonthCode && i.posting_month_code <= toMonthCode)
            .OrderByDescending(i => i.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoice by code.
    /// </summary>
    /// <param name="invoiceCode">Invoice code</param>
    /// <returns>Invoice if found</returns>
    public async Task<Invoice?> GetByCodeAsync(int invoiceCode)
    {
        return await _context.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.invoice_code == invoiceCode);
    }

    /// <summary>
    /// Update invoice department.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="departmentCode">New department code</param>
    /// <returns>Updated invoice</returns>
    public async Task<Invoice> UpdateDepartmentAsync(int invoiceCode, short departmentCode)
    {
        var invoice = await _context.Invoices.FindAsync(invoiceCode);
        if (invoice == null)
            throw new ArgumentException($"Invoice with code {invoiceCode} not found");

        invoice.department_code = departmentCode;
        
        await _context.SaveChangesAsync();
        return invoice;
    }
}