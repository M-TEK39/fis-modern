namespace FIS.Web.Components.Shared;

public sealed record DataPreviewColumn(string Key, string Label);

public sealed record DataPreviewFilter(string Label, string ColumnKey, string Value);
