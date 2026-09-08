using System.Text.Json;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Data.Seeders;

public class WorkflowTemplateSeeder
{
    private readonly FisDbContext _context;
    private readonly ILogger<WorkflowTemplateSeeder> _logger;

    public WorkflowTemplateSeeder(FisDbContext context, ILogger<WorkflowTemplateSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            var existingTemplates = await _context
                .Set<WorkflowTemplate>()
                .Where(t => !t.is_deleted)
                .ToListAsync();

            if (existingTemplates.Any())
            {
                _logger.LogInformation(
                    "Workflow templates already seeded ({Count} templates exist)",
                    existingTemplates.Count
                );
                return;
            }

            var templates = GetCommonTemplates();
            foreach (var template in templates)
            {
                _context.Set<WorkflowTemplate>().Add(template);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} workflow templates", templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding workflow templates");
            throw;
        }
    }

    private List<WorkflowTemplate> GetCommonTemplates()
    {
        var systemUserId = 1;
        var now = DateTime.Now;

        return new List<WorkflowTemplate>
        {
            new WorkflowTemplate
            {
                TemplateName = "Vehicle Accident Approval Workflow",
                Category = "Fleet Management",
                Description = "Complete workflow for handling vehicle accidents",
                IsActive = true,
                Version = 1,
                TemplateData = "[]",
                date_created = now,
                created_by_user_code = systemUserId,
                is_deleted = false,
            },
            new WorkflowTemplate
            {
                TemplateName = "Equipment Request & Approval",
                Category = "Procurement",
                Description = "Standard workflow for equipment requisition",
                IsActive = true,
                Version = 1,
                TemplateData = "[]",
                date_created = now,
                created_by_user_code = systemUserId,
                is_deleted = false,
            },
            new WorkflowTemplate
            {
                TemplateName = "Document Review & Sign-off",
                Category = "Administration",
                Description = "Multi-stage document review and approval",
                IsActive = true,
                Version = 1,
                TemplateData = "[]",
                date_created = now,
                created_by_user_code = systemUserId,
                is_deleted = false,
            },
            new WorkflowTemplate
            {
                TemplateName = "Incident Report & Investigation",
                Category = "Safety",
                Description = "Workflow for incident reporting and investigation",
                IsActive = true,
                Version = 1,
                TemplateData = "[]",
                date_created = now,
                created_by_user_code = systemUserId,
                is_deleted = false,
            },
            new WorkflowTemplate
            {
                TemplateName = "Maintenance Request Processing",
                Category = "Fleet Management",
                Description = "Standard workflow for vehicle maintenance requests",
                IsActive = true,
                Version = 1,
                TemplateData = "[]",
                date_created = now,
                created_by_user_code = systemUserId,
                is_deleted = false,
            },
        };
    }
}
