using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json;
using System.Text;

namespace FIS.Web.Services;

public class VehicleApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<VehicleApiService> _logger;

    public VehicleApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthorizationHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<PagedResult<VehicleDto>> GetVehiclesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? search = null
    )
    {
        try
        {
            AddAuthorizationHeader();
            
            var query = $"api/vehicles?pageNumber={pageNumber}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                var paged = JsonSerializer.Deserialize<PagedResult<VehicleDto>>(content, jsonOptions);
                if (paged is not null)
                {
                    return paged;
                }
            }
            catch (JsonException)
            {
                // fallback below
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<VehicleDto>>(content, jsonOptions) ?? new List<VehicleDto>();
                return new PagedResult<VehicleDto>
                {
                    Data = list,
                    TotalCount = list.Count,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing vehicles response: {Content}", content);
                return new PagedResult<VehicleDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicles");
            return new PagedResult<VehicleDto>();
        }
    }

    public async Task<VehicleDto?> GetVehicleAsync(int vmfCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VehicleDto>($"api/vehicles/{vmfCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle {VmfCode}", vmfCode);
            return null;
        }
    }

    public async Task<List<VehicleDto>> SearchVehiclesAsync(string searchTerm)
    {
        try
        {
            AddAuthorizationHeader();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<VehicleDto>();
            }

            var url = $"api/vehicles/search?searchTerm={Uri.EscapeDataString(searchTerm)}";
            var result = await _httpClient.GetFromJsonAsync<List<VehicleDto>>(url);
            return result ?? new List<VehicleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicles with term {SearchTerm}", searchTerm);
            return new List<VehicleDto>();
        }
    }

    public async Task<List<VehicleStatusReportRowDto>> GetNewInServiceReportAsync()
    {
        var result = await GetNewInServiceReportAsync(new NewInServiceReportFilter());
        return result.rows;
    }

    public async Task<VehicleStatusReportResult> GetNewInServiceReportAsync(NewInServiceReportFilter filter)
    {
        try
        {
            AddAuthorizationHeader();
            var queryParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.search))
            {
                queryParts.Add($"search={Uri.EscapeDataString(filter.search.Trim())}");
            }
            if (filter.location_code.HasValue)
            {
                queryParts.Add($"location_code={filter.location_code.Value}");
            }
            if (filter.type_code.HasValue)
            {
                queryParts.Add($"type_code={filter.type_code.Value}");
            }
            if (filter.make_code.HasValue)
            {
                queryParts.Add($"make_code={filter.make_code.Value}");
            }
            if (filter.model_code.HasValue)
            {
                queryParts.Add($"model_code={filter.model_code.Value}");
            }
            if (filter.vehicle_status_code.HasValue)
            {
                queryParts.Add($"vehicle_status_code={filter.vehicle_status_code.Value}");
            }

            var query = "api/report/new-in-service";
            if (queryParts.Count > 0)
            {
                query += "?" + string.Join("&", queryParts);
            }

            var response = await _httpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync();
            return ParseStatusReportResult(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading in-service report");
            return new VehicleStatusReportResult();
        }
    }

    public async Task<List<VehicleRemarkDto>> GetVehicleRemarksAsync(int vmfCode)
    {
        try
        {
            AddAuthorizationHeader();
            var list = await _httpClient.GetFromJsonAsync<List<VehicleRemarkDto>>($"api/vehicles/{vmfCode}/remarks");
            return list ?? new List<VehicleRemarkDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading remarks for vehicle {VmfCode}", vmfCode);
            return new List<VehicleRemarkDto>();
        }
    }

    public async Task<FinanceApiResult> AddVehicleRemarkAsync(int vmfCode, string category, string text)
    {
        return await SendVehicleRemarkActionAsync(
            HttpMethod.Post,
            $"api/vehicles/{vmfCode}/remarks",
            new
            {
                remark_category = category,
                remark_text = text
            });
    }

    public async Task<FinanceApiResult> ResolveVehicleRemarkAsync(int vmfCode, int remarkId, string resolutionNotes)
    {
        return await SendVehicleRemarkActionAsync(
            HttpMethod.Post,
            $"api/vehicles/{vmfCode}/remarks/{remarkId}/resolve",
            new
            {
                resolution_notes = resolutionNotes
            });
    }

    public async Task<VehicleStatusChangeResult> ChangeVehicleStatusAsync(
        int vmfCode,
        short newStatusCode,
        short? siteCode,
        DateTime? effectiveDate,
        string? notes)
    {
        try
        {
            AddAuthorizationHeader();

            var payload = new
            {
                new_status_code = newStatusCode,
                site_code = siteCode,
                effective_date = effectiveDate,
                notes
            };

            var response = await _httpClient.PatchAsJsonAsync($"api/vehicles/{vmfCode}/status", payload);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new VehicleStatusChangeResult
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    Message = ExtractApiErrorMessage(body) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode})."
                };
            }

            var parsed = JsonSerializer.Deserialize<VehicleStatusChangeApiResponse>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                });

            return new VehicleStatusChangeResult
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                Message = "Request completed successfully.",
                new_status_code = parsed?.new_status_code,
                new_status_description = parsed?.new_status_description,
                effective_date = parsed?.effective_date,
                location_code = parsed?.location_code,
                closed_contract_code = parsed?.closed_contract_code,
                actions_performed = parsed?.actions_performed ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing vehicle status for {VmfCode}", vmfCode);
            return new VehicleStatusChangeResult
            {
                Success = false,
                Message = "Request failed."
            };
        }
    }

    public async Task<VehicleLicenceHistoryResult> GetVehicleLicenceHistoryAsync(int vmfCode)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetFromJsonAsync<VehicleLicenceHistoryApiResponse>(
                $"api/vehicles/{vmfCode}/licence/history");

            return new VehicleLicenceHistoryResult
            {
                vmf_code = response?.vmf_code ?? vmfCode,
                fleet_number = response?.fleet_number,
                registration_number = response?.registration_number,
                history = response?.history ?? new List<VehicleLicenceHistoryEntryDto>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading licence history for vehicle {VmfCode}", vmfCode);
            return new VehicleLicenceHistoryResult
            {
                vmf_code = vmfCode,
                history = new List<VehicleLicenceHistoryEntryDto>()
            };
        }
    }

    public async Task<bool> CreateVehicleAsync(VehicleDto vehicle, bool recalculateTariff = false)
    {
        try
        {
            var createRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                site_code = vehicle.site_code,
                fleet_number = vehicle.fleet_number,
                registration_number = vehicle.registration_number,
                engine_number_1 = vehicle.engine_number_1,
                chassis_number = vehicle.chassis_number,
                take_on_date = vehicle.take_on_date,
                take_on_odo = vehicle.take_on_odo ?? 0,
                current_odo = vehicle.current_odo ?? 0,
                tare = vehicle.tare,
                gvm = vehicle.gvm,
                year_manufactured = vehicle.year_manufactured,
                colour = vehicle.colour,
                purchase_date = vehicle.purchase_date,
                purchase_amount = vehicle.purchase_amount,
                purchased_from = vehicle.purchased_from,
                invoice_number = vehicle.invoice_number,
                ifms_vehicle_register_number = vehicle.ifms_vehicle_register_number,
                natis_model_number = vehicle.natis_model_number,
                service_last_done = vehicle.service_last_done,
                service_last_odo = vehicle.service_last_odo,
                cof_last_done = vehicle.cof_last_done,
                cof_required = vehicle.cof_required,
                cof_number = vehicle.cof_number,
                cof_amount = vehicle.Cof_amount,
                extended_service = vehicle.extended_service,
                recalculate_tariff = recalculateTariff
            };

            var response = await _httpClient.PostAsJsonAsync("api/vehicles", createRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Error creating vehicle. Status: {StatusCode}, Response: {Content}",
                    response.StatusCode,
                    errorContent
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return false;
        }
    }

    public async Task<bool> UpdateVehicleAsync(int vmfCode, VehicleDto vehicle, bool recalculateTariff = false)
    {
        try
        {
            var updateRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                site_code = vehicle.site_code,
                fleet_number = vehicle.fleet_number,
                registration_number = vehicle.registration_number,
                engine_number_1 = vehicle.engine_number_1,
                chassis_number = vehicle.chassis_number,
                take_on_odo = vehicle.take_on_odo,
                current_odo = vehicle.current_odo,
                tare = vehicle.tare,
                gvm = vehicle.gvm,
                year_manufactured = vehicle.year_manufactured,
                colour = vehicle.colour,
                purchase_date = vehicle.purchase_date,
                purchase_amount = vehicle.purchase_amount,
                purchased_from = vehicle.purchased_from,
                invoice_number = vehicle.invoice_number,
                ifms_vehicle_register_number = vehicle.ifms_vehicle_register_number,
                natis_model_number = vehicle.natis_model_number,
                service_last_done = vehicle.service_last_done,
                service_last_odo = vehicle.service_last_odo,
                cof_last_done = vehicle.cof_last_done,
                cof_required = vehicle.cof_required,
                cof_number = vehicle.cof_number,
                cof_amount = vehicle.Cof_amount,
                extended_service = vehicle.extended_service,
                recalculate_tariff = recalculateTariff
            };

            var response = await _httpClient.PutAsJsonAsync($"api/vehicles/{vmfCode}", updateRequest);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Error updating vehicle {VmfCode}. Status: {StatusCode}, Response: {Content}",
                    vmfCode,
                    response.StatusCode,
                    errorContent
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> DeleteVehicleAsync(int vmfCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/vehicles/{vmfCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<FinanceApiResult> UpdateInvoiceNumberAsync(int vmfCode, string? invoiceNumber)
    {
        try
        {
            AddAuthorizationHeader();

            var payload = new
            {
                invoice_number = string.IsNullOrWhiteSpace(invoiceNumber)
                    ? null
                    : invoiceNumber.Trim()
            };

            var response = await _httpClient.PatchAsJsonAsync($"api/vehicles/{vmfCode}/invoice", payload);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = $"api/vehicles/{vmfCode}/invoice",
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : ExtractApiErrorMessage(body) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating invoice number for vehicle {VmfCode}", vmfCode);
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = $"api/vehicles/{vmfCode}/invoice",
                Message = "Request failed."
            };
        }
    }

    public async Task<List<VehicleAuthorizationDto>> GetPendingAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/pending");

    public async Task<List<VehicleAuthorizationDto>> GetAuthorizedAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/authorized");

    public async Task<List<VehicleAuthorizationDto>> GetRejectedAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/rejected");

    public async Task<VehicleAuthorizationDto?> GetVehicleAuthorizationAsync(int id)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/vehicle/authorization/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<VehicleAuthorizationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle authorization {Id}", id);
            return null;
        }
    }

    public async Task<FinanceApiResult> ApproveVehicleAuthorizationAsync(int id, string? comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/approve",
            new { comment });
    }

    public async Task<FinanceApiResult> RejectVehicleAuthorizationAsync(int id, string rejectionReason, string? comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/reject",
            new { rejectionReason, comment });
    }

    public async Task<FinanceApiResult> AddVehicleAuthorizationCommentAsync(int id, string comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/comment",
            new { comment });
    }

    private async Task<List<VehicleAuthorizationDto>> GetVehicleAuthorizationsAsync(string endpoint)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<VehicleAuthorizationDto>>()
                ?? new List<VehicleAuthorizationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle authorization list.");
            return new List<VehicleAuthorizationDto>();
        }
    }

    private async Task<FinanceApiResult> PostVehicleAuthorizationActionAsync(string endpoint, object payload)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vehicle authorization action failed. Status: {Status}. Body: {Body}",
                    response.StatusCode,
                    body);
            }

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : SanitizeUserMessage(ExtractApiErrorMessage(body)) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting vehicle authorization action.");
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    private static string? ExtractApiErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorValue))
            {
                var errorMessage = errorValue.GetString();
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    return errorMessage.Trim();
                }
            }

            if (root.TryGetProperty("message", out var messageValue))
            {
                var message = messageValue.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }
        catch
        {
            // Ignore parse failures and keep fallback status message.
        }

        return null;
    }

    private async Task<FinanceApiResult> SendVehicleRemarkActionAsync(HttpMethod method, string endpoint, object payload)
    {
        try
        {
            AddAuthorizationHeader();
            using var request = new HttpRequestMessage(method, endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : SanitizeUserMessage(ExtractApiErrorMessage(body)) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vehicle remark request failed.");
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    private static string? SanitizeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        var endpointIndex = trimmed.IndexOf("Endpoint:", StringComparison.OrdinalIgnoreCase);
        if (endpointIndex >= 0)
        {
            trimmed = trimmed[..endpointIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static VehicleStatusReportResult ParseStatusReportResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new VehicleStatusReportResult();
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        try
        {
            var directList = JsonSerializer.Deserialize<List<VehicleStatusReportRowDto>>(json, options);
            if (directList is { Count: > 0 })
            {
                return new VehicleStatusReportResult
                {
                    total_count = directList.Count,
                    rows = directList
                };
            }
        }
        catch
        {
            // fallback below
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var rows = JsonSerializer.Deserialize<List<VehicleStatusReportRowDto>>(root.GetRawText(), options)
                    ?? new List<VehicleStatusReportRowDto>();
                return new VehicleStatusReportResult
                {
                    total_count = rows.Count,
                    rows = rows
                };
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("vehicles", out var vehicles) && vehicles.ValueKind == JsonValueKind.Array)
                {
                    var rows = JsonSerializer.Deserialize<List<VehicleStatusReportRowDto>>(vehicles.GetRawText(), options)
                        ?? new List<VehicleStatusReportRowDto>();
                    var count = root.TryGetProperty("total_count", out var totalCountValue)
                        && totalCountValue.ValueKind == JsonValueKind.Number
                        && totalCountValue.TryGetInt32(out var parsedTotal)
                        ? parsedTotal
                        : rows.Count;
                    return new VehicleStatusReportResult
                    {
                        total_count = count,
                        rows = rows
                    };
                }

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var rows = JsonSerializer.Deserialize<List<VehicleStatusReportRowDto>>(data.GetRawText(), options)
                        ?? new List<VehicleStatusReportRowDto>();
                    return new VehicleStatusReportResult
                    {
                        total_count = rows.Count,
                        rows = rows
                    };
                }

                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    var rows = JsonSerializer.Deserialize<List<VehicleStatusReportRowDto>>(items.GetRawText(), options)
                        ?? new List<VehicleStatusReportRowDto>();
                    return new VehicleStatusReportResult
                    {
                        total_count = rows.Count,
                        rows = rows
                    };
                }
            }
        }
        catch
        {
            // Ignore and return empty list.
        }

        return new VehicleStatusReportResult();
    }
}

public class NewInServiceReportFilter
{
    public string? search { get; set; }
    public short? location_code { get; set; }
    public short? type_code { get; set; }
    public short? make_code { get; set; }
    public short? model_code { get; set; }
    public short? vehicle_status_code { get; set; }
}

public class VehicleStatusReportResult
{
    public int total_count { get; set; }
    public List<VehicleStatusReportRowDto> rows { get; set; } = new();
}

public class VehicleStatusReportRowDto
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public string? status_description { get; set; }
    public string? status_text { get; set; }
    public short? vehicle_status_code { get; set; }
    public short? type_code { get; set; }
    public string? type_description { get; set; }
    public short? model_code { get; set; }
    public string? make_description { get; set; }
    public string? model_description { get; set; }
    public short? location_code { get; set; }
    public string? site_name { get; set; }
    public string? invoice_number { get; set; }
    public string? chassis_number { get; set; }
    public string? engine_number_1 { get; set; }
    public DateTime? vehicle_status_date { get; set; }
    public string? location_description { get; set; }
    public string? model_name { get; set; }
    public VehicleRemarkDto? active_remark { get; set; }
}

public class VehicleRemarkDto
{
    public int remark_id { get; set; }
    public int vmf_code { get; set; }
    public string? remark_category { get; set; }
    public string? remark_text { get; set; }
    public DateTime? date_created { get; set; }
    public DateTime? date_resolved { get; set; }
    public string? resolution_notes { get; set; }
}

public class VehicleLicenceHistoryResult
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public List<VehicleLicenceHistoryEntryDto> history { get; set; } = new();
}

public class VehicleLicenceHistoryEntryDto
{
    public int licence_history_id { get; set; }
    public DateTime? licence_due_date { get; set; }
    public string? lic_register_number { get; set; }
    public string? lic_registration_doc { get; set; }
    public string? licence_comments { get; set; }
    public DateTime? cof_last_done { get; set; }
    public string? cof_required { get; set; }
    public int? tare { get; set; }
    public string? Licence_receiver { get; set; }
    public string? Licence_receiver_id { get; set; }
    public string? Licence_receiver_tel { get; set; }
    public short? Licence_receiver_site { get; set; }
    public DateTime? Licence_date_taken { get; set; }
    public DateTime captured_at { get; set; }
    public string? captured_by_user_email { get; set; }
    public string? update_notes { get; set; }
}

public class VehicleStatusChangeResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public short? new_status_code { get; set; }
    public string? new_status_description { get; set; }
    public DateTime? effective_date { get; set; }
    public short? location_code { get; set; }
    public int? closed_contract_code { get; set; }
    public List<string> actions_performed { get; set; } = new();
}

file sealed class VehicleLicenceHistoryApiResponse
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public List<VehicleLicenceHistoryEntryDto>? history { get; set; }
}

file sealed class VehicleStatusChangeApiResponse
{
    public short? new_status_code { get; set; }
    public string? new_status_description { get; set; }
    public DateTime? effective_date { get; set; }
    public short? location_code { get; set; }
    public int? closed_contract_code { get; set; }
    public List<string>? actions_performed { get; set; }
}
