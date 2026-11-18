namespace MDGsm.Client;

/// <summary>Non-sensitive technical metadata returned by GET /api/v1/system/info.</summary>
public sealed record SystemInfoDto(string Product, string ApiVersion, string ReleaseStage);
