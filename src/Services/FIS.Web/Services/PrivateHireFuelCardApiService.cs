using System.Net.Http.Json;

namespace FIS.Web.Services;

public sealed class PrivateHireFuelCardApiService
{
    private readonly HttpClient _httpClient;

    public PrivateHireFuelCardApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<PrivateHireFuelCardRecord>> GetByRegistrationAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return new List<PrivateHireFuelCardRecord>();
        }

        try
        {
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
