using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class MerchantApiService(HttpClient httpClient, TokenService tokenService, ILogger<MerchantApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/merchant";

    public Task<List<MerchantDto>> GetAllAsync() => GetListAsync<MerchantDto>(BasePath);
    public Task<MerchantDto?> GetByIdAsync(int id) => GetAsync<MerchantDto>($"{BasePath}/{id}");
    public Task<MerchantDto?> CreateAsync(MerchantCreateDto payload) =>
        PostAsync<MerchantCreateDto, MerchantDto>(BasePath, payload);
    public Task<MerchantDto?> UpdateAsync(int id, MerchantCreateDto payload) =>
        PutAsync<MerchantCreateDto, MerchantDto>($"{BasePath}/{id}", payload);
    public Task DeleteAsync(int id) => DeleteAsync($"{BasePath}/{id}");
}
