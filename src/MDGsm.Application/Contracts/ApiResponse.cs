namespace MDGsm.Application.Contracts;

/// <summary>Stable success envelope for versioned HTTP API responses.</summary>
public sealed record ApiResponse<T>(T Data, string RequestId);
