using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class TroubleshootApiService(HttpClient httpClient, TokenService tokenService, ILogger<TroubleshootApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/troubleshoot";

    public Task<List<TroubleshootUserDto>> GetUsersAsync() =>
        GetListAsync<TroubleshootUserDto>($"{BasePath}/users");

    public Task<List<TroubleshootSiteUserDto>> GetDepartmentSitesAsync() =>
        GetListAsync<TroubleshootSiteUserDto>($"{BasePath}/departmentsites");

    public async Task<List<TroubleshootLogEntryDto>> SearchLogsAsync(TroubleshootLogSearchRequest request) =>
        await PostAsync<TroubleshootLogSearchRequest, List<TroubleshootLogEntryDto>>($"{BasePath}/log/search", request)
        ?? new List<TroubleshootLogEntryDto>();

    public async Task UpdateLogsAsync() =>
        await PostAsync<object, object>($"{BasePath}/log/update", new { });

    public async Task<List<TroubleshootLogEntryDto>> GetGeneralReportsAsync(TroubleshootReportFilter filter) =>
        await PostAsync<TroubleshootReportFilter, List<TroubleshootLogEntryDto>>($"{BasePath}/reports/general", filter)
        ?? new List<TroubleshootLogEntryDto>();

    public async Task<List<OdometerCorrectionResultDto>> SearchOdometerCorrectionsAsync(OdometerCorrectionSearchRequest request) =>
        await PostAsync<OdometerCorrectionSearchRequest, List<OdometerCorrectionResultDto>>($"{BasePath}/odometer/search", request)
        ?? new List<OdometerCorrectionResultDto>();

    public async Task RemoveTripsWithoutRoutesAsync(RemoveTripsRequest request) =>
        await PostAsync<RemoveTripsRequest, object>($"{BasePath}/remove-trips-no-routes", request);

    public Task<List<ApproverRankDto>> GetApproverRanksAsync() =>
        GetListAsync<ApproverRankDto>("api/authorisers/ranks");

    public async Task<List<ApproverRankDto>> SaveApproverRanksAsync(List<ApproverRankDto> ranks) =>
        await PostAsync<List<ApproverRankDto>, List<ApproverRankDto>>("api/authorisers/ranks", ranks)
        ?? new List<ApproverRankDto>();

    public async Task<List<VehicleDto>> GetVehicleMasterEditAsync(VehicleMasterEditRequest request) =>
        await PostAsync<VehicleMasterEditRequest, List<VehicleDto>>($"{BasePath}/vehicle-master-edit", request)
        ?? new List<VehicleDto>();

    public async Task UpdateRecoveredGgAsync(UpdateRecoveredGgRequest request) =>
        await PostAsync<UpdateRecoveredGgRequest, object>($"{BasePath}/update-recovered-gg", request);
}
