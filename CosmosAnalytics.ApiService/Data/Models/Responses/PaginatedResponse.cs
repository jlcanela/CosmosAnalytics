namespace CosmosAnalytics.ApiService
{
    public record PaginatedResponse<T>(
        List<T> Items,
        string? ContinuationToken,
        int Count
    );
}
