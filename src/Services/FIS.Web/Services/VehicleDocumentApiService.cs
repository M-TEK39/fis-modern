using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class VehicleDocumentApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<VehicleDocumentApiService> _logger;
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    public VehicleDocumentApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleDocumentApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<VehicleDocumentDto>> GetDocumentsAsync(int vmfCode, string? category = null)
    {
        try
        {
            AddAuthHeader();
            var path = $"api/vehicles/{vmfCode}/documents";
            if (!string.IsNullOrWhiteSpace(category))
            {
                path += $"?category={Uri.EscapeDataString(category.Trim())}";
            }

            var result = await _httpClient.GetFromJsonAsync<List<VehicleDocumentDto>>(path);
            return result ?? new List<VehicleDocumentDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading documents for vehicle {VmfCode}", vmfCode);
            return new List<VehicleDocumentDto>();
        }
    }

    public async Task<List<VehicleDocumentDto>> GetDocumentsByReferenceAsync(int vmfCode, string referenceType, int referenceId)
    {
        try
        {
            AddAuthHeader();
            var path = $"api/vehicles/{vmfCode}/documents/by-reference?type={Uri.EscapeDataString(referenceType)}&id={referenceId}";
            var result = await _httpClient.GetFromJsonAsync<List<VehicleDocumentDto>>(path);
            return result ?? new List<VehicleDocumentDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading documents by reference for vehicle {VmfCode}", vmfCode);
            return new List<VehicleDocumentDto>();
        }
    }

    public async Task<(bool Success, string Message)> UploadAsync(
        int vmfCode,
        IBrowserFile file,
        string category,
        string? referenceType,
        int? referenceId)
    {
        try
        {
            AddAuthHeader();
            using var content = new MultipartFormDataContent();
            await using var fileStream = file.OpenReadStream(MaxUploadBytes);
            using var streamContent = new StreamContent(fileStream);

            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

            content.Add(streamContent, "file", file.Name);
            content.Add(new StringContent(category), "category");

            if (!string.IsNullOrWhiteSpace(referenceType))
            {
                content.Add(new StringContent(referenceType), "reference_type");
            }

            if (referenceId.HasValue)
            {
                content.Add(new StringContent(referenceId.Value.ToString()), "reference_id");
            }

            var response = await _httpClient.PostAsync($"api/vehicles/{vmfCode}/documents", content);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Request completed successfully.");
            }

            var body = await response.Content.ReadAsStringAsync();
            return (false, string.IsNullOrWhiteSpace(body)
                ? $"Upload failed ({(int)response.StatusCode})."
                : body);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File upload stream error for vehicle {VmfCode}", vmfCode);
            return (false, "Upload failed. Ensure file is under 20 MB.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document for vehicle {VmfCode}", vmfCode);
            return (false, "Upload failed.");
        }
    }

    public async Task<VehicleDocumentDownloadResult?> DownloadAsync(int vmfCode, int documentId)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.GetAsync($"api/vehicles/{vmfCode}/documents/{documentId}/download");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? $"document_{documentId}";

            return new VehicleDocumentDownloadResult
            {
                FileName = fileName.Trim('"'),
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                Content = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading vehicle document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(int vmfCode, int documentId)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.DeleteAsync($"api/vehicles/{vmfCode}/documents/{documentId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle document {DocumentId}", documentId);
            return false;
        }
    }
}
