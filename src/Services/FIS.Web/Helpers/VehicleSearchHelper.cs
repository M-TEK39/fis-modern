using FIS.Web.Models;

namespace FIS.Web.Helpers;

public static class VehicleSearchHelper
{
    public const string GgMode = "GG";
    public const string GpMode = "GP";
    public const string EngineMode = "ENGINE";
    public const string VinMode = "VIN";
    public const string InvoiceMode = "INVOICE";

    public static string NormalizeMode(string? mode)
        => mode?.Trim().ToUpperInvariant() switch
        {
            GpMode => GpMode,
            EngineMode => EngineMode,
            "CHASSIS" => VinMode,
            VinMode => VinMode,
            InvoiceMode => InvoiceMode,
            _ => GgMode
        };

    public static string GetSearchLabel(string? mode)
        => NormalizeMode(mode) switch
        {
            GpMode => "GP Number",
            EngineMode => "Engine Number",
            VinMode => "VIN / Chassis Number",
            InvoiceMode => "Invoice Number",
            _ => "GG Number"
        };

    public static string GetSearchPlaceholder(string? mode)
        => NormalizeMode(mode) switch
        {
            GpMode => "GP number",
            EngineMode => "Engine number",
            VinMode => "VIN / chassis number",
            InvoiceMode => "Invoice number",
            _ => "GG number"
        };

    public static List<VehicleDto> RankVehicleMatches(IEnumerable<VehicleDto> matches, string query, string? mode)
        => RankMatches(
            matches,
            query,
            mode,
            vehicle => vehicle.fleet_number,
            vehicle => vehicle.registration_number,
            vehicle => vehicle.engine_number_1,
            vehicle => vehicle.chassis_number,
            vehicle => vehicle.invoice_number,
            vehicle => vehicle.vmf_code);

    public static bool MatchesVehicle(VehicleDto vehicle, string query, string? mode)
        => ContainsMatch(
            NormalizeMode(mode),
            query.Trim(),
            vehicle.fleet_number,
            vehicle.registration_number,
            vehicle.engine_number_1,
            vehicle.chassis_number,
            vehicle.invoice_number);

    public static List<VehicleLookupDto> RankVehicleLookupMatches(IEnumerable<VehicleLookupDto> matches, string query, string? mode)
        => RankMatches(
            matches,
            query,
            mode,
            vehicle => vehicle.GGNumber ?? vehicle.FleetNumber,
            vehicle => vehicle.RegistrationNumber,
            vehicle => vehicle.EngineNumber,
            vehicle => vehicle.ChassisNumber,
            vehicle => vehicle.InvoiceNumber,
            vehicle => vehicle.VmfCode);

    public static bool MatchesVehicleLookup(VehicleLookupDto vehicle, string query, string? mode)
        => ContainsMatch(
            NormalizeMode(mode),
            query.Trim(),
            vehicle.GGNumber ?? vehicle.FleetNumber,
            vehicle.RegistrationNumber,
            vehicle.EngineNumber,
            vehicle.ChassisNumber,
            vehicle.InvoiceNumber);

    public static string? GetSearchValue(VehicleLookupDto vehicle, string? mode)
        => GetSearchValue(
            NormalizeMode(mode),
            vehicle.GGNumber ?? vehicle.FleetNumber,
            vehicle.RegistrationNumber,
            vehicle.EngineNumber,
            vehicle.ChassisNumber,
            vehicle.InvoiceNumber);

    public static string? GetSearchValue(VehicleDto vehicle, string? mode)
        => GetSearchValue(
            NormalizeMode(mode),
            vehicle.fleet_number,
            vehicle.registration_number,
            vehicle.engine_number_1,
            vehicle.chassis_number,
            vehicle.invoice_number);

    private static List<TVehicle> RankMatches<TVehicle>(
        IEnumerable<TVehicle> matches,
        string query,
        string? mode,
        Func<TVehicle, string?> ggSelector,
        Func<TVehicle, string?> gpSelector,
        Func<TVehicle, string?> engineSelector,
        Func<TVehicle, string?> vinSelector,
        Func<TVehicle, string?> invoiceSelector,
        Func<TVehicle, int> vmfSelector)
    {
        var keyword = query.Trim();
        var normalizedMode = NormalizeMode(mode);

        return matches
            .Where(vehicle => ContainsMatch(
                normalizedMode,
                keyword,
                ggSelector(vehicle),
                gpSelector(vehicle),
                engineSelector(vehicle),
                vinSelector(vehicle),
                invoiceSelector(vehicle)))
            .OrderByDescending(vehicle => ExactMatch(
                normalizedMode,
                keyword,
                ggSelector(vehicle),
                gpSelector(vehicle),
                engineSelector(vehicle),
                vinSelector(vehicle),
                invoiceSelector(vehicle)))
            .ThenBy(vmfSelector)
            .ToList();
    }

    private static bool ContainsMatch(
        string mode,
        string keyword,
        string? ggNumber,
        string? gpNumber,
        string? engineNumber,
        string? vinNumber,
        string? invoiceNumber)
        => mode switch
        {
            GpMode => ContainsValue(gpNumber, keyword),
            EngineMode => ContainsValue(engineNumber, keyword),
            VinMode => ContainsValue(vinNumber, keyword),
            InvoiceMode => ContainsValue(invoiceNumber, keyword),
            _ => ContainsValue(ggNumber, keyword)
        };

    private static bool ExactMatch(
        string mode,
        string keyword,
        string? ggNumber,
        string? gpNumber,
        string? engineNumber,
        string? vinNumber,
        string? invoiceNumber)
        => mode switch
        {
            GpMode => EqualsValue(gpNumber, keyword),
            EngineMode => EqualsValue(engineNumber, keyword),
            VinMode => EqualsValue(vinNumber, keyword),
            InvoiceMode => EqualsValue(invoiceNumber, keyword),
            _ => EqualsValue(ggNumber, keyword)
        };

    private static string? GetSearchValue(
        string mode,
        string? ggNumber,
        string? gpNumber,
        string? engineNumber,
        string? vinNumber,
        string? invoiceNumber)
        => mode switch
        {
            GpMode => gpNumber,
            EngineMode => engineNumber,
            VinMode => vinNumber,
            InvoiceMode => invoiceNumber,
            _ => ggNumber
        };

    private static bool ContainsValue(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsValue(string? value, string keyword)
        => string.Equals(value?.Trim(), keyword, StringComparison.OrdinalIgnoreCase);
}
