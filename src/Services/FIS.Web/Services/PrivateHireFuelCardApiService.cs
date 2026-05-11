using System.Net.Http.Json;

namespace FIS.Web.Services;

public sealed class PrivateHireFuelCardApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;

    public PrivateHireFuelCardApiService(HttpClient httpClient, TokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token)) return;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<PrivateHireFuelCardRecord>> GetByRegistrationAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return new List<PrivateHireFuelCardRecord>();
        }

        try
        {
            AddAuthHeader();
            return await _httpClient.GetFromJsonAsync<List<PrivateHireFuelCardRecord>>(
                       $"api/privatehirefuelcard/registration/{Uri.EscapeDataString(registrationNumber.Trim())}")
                   ?? new List<PrivateHireFuelCardRecord>();
        }
        catch
        {
            return new List<PrivateHireFuelCardRecord>();
        }
    }

    public async Task<bool> DeleteAsync(int privateHireFuelCardCode)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.DeleteAsync($"api/privatehirefuelcard/{privateHireFuelCardCode}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class PrivateHireFuelCardRecord
{
    public int PHFuel_card_code { get; set; }
    public int phv_code { get; set; }
    public short? Counter { get; set; }
    public string? card_number { get; set; }
}
