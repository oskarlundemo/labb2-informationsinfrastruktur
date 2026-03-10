namespace PlacementService.Ui.Services;

public sealed record ApiResult<T>(T? Data, string? Error)
{
    public bool IsSuccess => Error is null;
}
