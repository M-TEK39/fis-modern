using FIS.Core.Application.Interfaces.Repositories;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for InvoiceItem entity operations.
/// Handles invoice item persistence and retrieval operations.
/// </summary>
public class InvoiceItemRepository : IInvoiceItemRepository
{
    private readonly FisDbContext _context;

    public InvoiceItemRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get an invoice item by its ID.
    /// </summary>
    /// <param name="id">Invoice item identifier</param>
    /// <returns>Invoice item if found</returns>
    public async Task<InvoiceItem?> GetByIdAsync(int id)
    {
        return await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .FirstOrDefaultAsync(ii => ii.item_code == id);
    }

    /// <summary>
    /// Get all invoice items.
    /// </summary>
    /// <returns>List of all invoice items</returns>
    public async Task<List<InvoiceItem>> GetAllAsync()
    {
        return await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .OrderBy(ii => ii.invoice_code)
            .ThenBy(ii => ii.item_code)
            .ToListAsync();
    }

    /// <summary>
    /// Add a new invoice item.
    /// </summary>
    /// <param name="invoiceItem">Invoice item to add</param>
    /// <returns>Added invoice item</returns>
    public async Task<InvoiceItem> AddAsync(InvoiceItem invoiceItem)
    {
        if (invoiceItem == null)
            throw new ArgumentNullException(nameof(invoiceItem));

        _context.InvoiceItems.Add(invoiceItem);
        await _context.SaveChangesAsync();
        return invoiceItem;
    }

    /// <summary>
    /// Update an existing invoice item.
    /// </summary>
    /// <param name="invoiceItem">Invoice item to update</param>
    /// <returns>Updated invoice item</returns>
    public async Task<InvoiceItem> UpdateAsync(InvoiceItem invoiceItem, int currentUserId)
    {
        if (invoiceItem == null)
            throw new ArgumentNullException(nameof(invoiceItem));

        var existing = await _context.InvoiceItems.FindAsync(invoiceItem.item_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"InvoiceItem with item_code {invoiceItem.item_code} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(invoiceItem);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete an invoice item by ID.
    /// </summary>
    /// <param name="id">Invoice item identifier</param>
    /// <returns>True if deleted</returns>
    public async Task<bool> DeleteAsync(int id, int currentUserId)
    {
        var invoiceItem = await _context.InvoiceItems.FindAsync(id);
        if (invoiceItem == null)
            return false;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('dbo.invoice_item', 'is_deleted') IS NOT NULL
                    UPDATE [dbo].[invoice_item]
                    SET [is_deleted] = 1
                    WHERE [item_code] = @itemCode;
                ELSE
                    DELETE FROM [dbo].[invoice_item]
                    WHERE [item_code] = @itemCode;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@itemCode";
            parameter.DbType = DbType.Int32;
            parameter.Value = id;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(invoiceItem).State = EntityState.Detached;
            return true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Get all items for a specific invoice.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <returns>List of invoice items</returns>
    public async Task<List<InvoiceItem>> GetByInvoiceIdAsync(int invoiceCode)
    {
        return await _context
            .InvoiceItems.Where(ii => ii.invoice_code == invoiceCode)
            .OrderBy(ii => ii.item_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoice items by contract type and date range.
    /// Note: Modified from contract_code to contract_type since contract_code doesn't exist in schema.
    /// </summary>
    /// <param name="contractType">Contract type</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items for the contract type in the period</returns>
    public async Task<List<InvoiceItem>> GetByContractTypeAndPeriodAsync(
        string contractType,
        DateTime fromDate,
        DateTime toDate
    )
    {
        var fromMonthCode = (short)(fromDate.Year * 100 + fromDate.Month);
        var toMonthCode = (short)(toDate.Year * 100 + toDate.Month);

        return await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .Where(ii =>
                ii.contract_type == contractType
                && ii.Invoice!.posting_month_code >= fromMonthCode
                && ii.Invoice!.posting_month_code <= toMonthCode
            )
            .OrderByDescending(ii => ii.Invoice!.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoice items by vehicle and date range.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items for the vehicle in the period</returns>
    public async Task<List<InvoiceItem>> GetByVehicleAndPeriodAsync(
        int vmfCode,
        DateTime fromDate,
        DateTime toDate
    )
    {
        return await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .Where(ii => ii.vmf_code == vmfCode)
            .OrderByDescending(ii => ii.Invoice!.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get invoice items by contract type and date range.
    /// Note: Using contract_type since item_type doesn't exist in schema.
    /// </summary>
    /// <param name="contractType">Contract type</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>List of invoice items of the specified contract type</returns>
    public async Task<List<InvoiceItem>> GetByTypeAndPeriodAsync(
        string contractType,
        DateTime fromDate,
        DateTime toDate
    )
    {
        var fromMonthCode = (short)(fromDate.Year * 100 + fromDate.Month);
        var toMonthCode = (short)(toDate.Year * 100 + toDate.Month);

        return await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .Where(ii =>
                ii.contract_type == contractType
                && ii.Invoice!.posting_month_code >= fromMonthCode
                && ii.Invoice!.posting_month_code <= toMonthCode
            )
            .OrderByDescending(ii => ii.Invoice!.posting_month_code)
            .ToListAsync();
    }

    /// <summary>
    /// Delete all items for an invoice (used for cancellations).
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <returns>Number of items deleted</returns>
    public async Task<int> DeleteByInvoiceIdAsync(int invoiceCode)
    {
        var items = await _context
            .InvoiceItems.Where(ii => ii.invoice_code == invoiceCode)
            .ToListAsync();

        _context.InvoiceItems.RemoveRange(items);
        await _context.SaveChangesAsync();
        return items.Count();
    }

    /// <summary>
    /// Get summary totals for invoice items by department.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>Summary totals</returns>
    public async Task<InvoiceItemSummary> GetSummaryByDepartmentAsync(
        int departmentCode,
        DateTime fromDate,
        DateTime toDate
    )
    {
        var items = await _context
            .InvoiceItems.Include(ii => ii.Invoice)
            .Where(ii => ii.Invoice!.department_code == departmentCode)
            .ToListAsync();

        // Filter by converted month codes since invoice table only has posting_month_code
        var fromMonthCode = (short)(fromDate.Year * 100 + fromDate.Month);
        var toMonthCode = (short)(toDate.Year * 100 + toDate.Month);
        items = items
            .Where(ii =>
                ii.Invoice!.posting_month_code >= fromMonthCode
                && ii.Invoice.posting_month_code <= toMonthCode
            )
            .ToList();

        var summary = new InvoiceItemSummary
        {
            DepartmentCode = departmentCode,
            FromDate = fromDate,
            ToDate = toDate,
            TotalFixedAmount = items.Sum(ii => ii.fixed_tariff_amount),
            TotalKilometerAmount = items.Sum(ii => ii.odo_tariff_amount),
            TotalAmount = items.Sum(ii =>
                ii.fixed_tariff_amount + ii.odo_tariff_amount + (ii.variable_cost ?? 0)
            ),
            ItemCount = items.Count(),
            ContractCount = items.Select(ii => ii.contract_type).Distinct().Count(),
        };

        return summary;
    }
}
