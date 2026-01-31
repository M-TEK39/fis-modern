using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class ClearanceApiService(HttpClient httpClient, TokenService tokenService, ILogger<ClearanceApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/clearance";

    public Task<List<ClearanceDto>> GetAllAsync() => GetListAsync<ClearanceDto>(BasePath);
    public Task<ClearanceDto?> GetByIdAsync(int id) => GetAsync<ClearanceDto>($"{BasePath}/{id}");
    public Task<List<ClearanceDto>> GetByVehicleAsync(int vmfCode) =>
        GetListAsync<ClearanceDto>($"{BasePath}/vehicle/{vmfCode}");
    public Task<ClearanceDto?> CreateAsync(ClearanceCreateDto payload) =>
        PostAsync<ClearanceCreateDto, ClearanceDto>(BasePath, payload);
    public Task<ClearanceDto?> UpdateAsync(int id, ClearanceCreateDto payload) =>
        PutAsync<ClearanceCreateDto, ClearanceDto>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
    public Task<ClearanceLookupResult?> LookupVehicleAsync(string lookup) =>
        GetAsync<ClearanceLookupResult>($"{BasePath}/lookup/{Uri.EscapeDataString(lookup)}");
}
