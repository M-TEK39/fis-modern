namespace FIS.Core.Domain.Enums;

/// <summary>
/// Status codes matching legacy GetVehicleTariff return values.
/// </summary>
public enum TariffStatus
{
    /// <summary>
    /// Valid tariff amount (> 0 or = 0 with valid reason).
    /// </summary>
    Valid = 0,

    /// <summary>
    /// Year manufactured not found (returns -1).
    /// </summary>
    YearNotFound = -1,

    /// <summary>
    /// No matching tariff for class/year/date combination (returns -2).
    /// </summary>
    NoMatch = -2,

    /// <summary>
    /// Incomplete tariff - missing overhead or maintenance components (returns -3).
    /// </summary>
    Incomplete = -3,

    /// <summary>
    /// Error during calculation.
    /// </summary>
    Error = -99
}

/// <summary>
/// Source of tariff calculation.
/// </summary>
public enum TariffSource
{
    /// <summary>
    /// No tariff found.
    /// </summary>
    None,

    /// <summary>
    /// Legacy tariff table (pre-2009).
    /// </summary>
    Legacy,

    /// <summary>
    /// Modern fin.vehicle_tariff table (post-2009).
    /// </summary>
    Modern,

    /// <summary>
    /// LeaseTariff table (lease-specific rates).
    /// </summary>
    Lease,

    /// <summary>
    /// Special business rule (0.00 tariff for GGMT internal, relief vehicles, etc.).
    /// </summary>
    SpecialRule,

    /// <summary>
    /// Error occurred during calculation.
    /// </summary>
    Error
}

/// <summary>
/// Type of tariff requested.
/// </summary>
public enum TariffType
{
    /// <summary>
    /// Fixed tariff (daily/monthly/hourly charges).
    /// </summary>
    Fixed,

    /// <summary>
    /// Kilometer tariff (per-kilometer charges).
    /// </summary>
    Kilos,

    /// <summary>
    /// Excess kilometer charges for lease vehicles.
    /// </summary>
    Excess
}

/// <summary>
/// Tariff system to use for calculation.
/// </summary>
public enum TariffSystem
{
    /// <summary>
    /// Legacy tariff table (pre-2009).
    /// </summary>
    Legacy,

    /// <summary>
    /// Modern fin.vehicle_tariff table (post-2009).
    /// </summary>
    Modern,

    /// <summary>
    /// LeaseTariff table (lease-specific).
    /// </summary>
    Lease
}