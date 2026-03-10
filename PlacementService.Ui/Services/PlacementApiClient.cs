using PlacementService.Ui.Models;

namespace PlacementService.Ui.Services;

public sealed class PlacementApiClient
{
    private readonly HttpClient _httpClient;

    public PlacementApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResult<PlacementSearchResponse>> SearchAsync(
        string query,
        string? region,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        // Step 1: build a dictionary with query, limit and offset, plus region if provided to act as the parameters for the API-query. Build a url variable to include the API endpoint and the query parameters
        // HINT: Use QueryHelpers.AddQueryString to append parameters to the base path

        // Step 2: call _httpClient.GetAsync() with the full URL and inspect the response.StatusCode. You should then handle the following responses correctly:
        //   - 200 OK: deserialize JSON into PlacementSearchResponse.
        // HINT: In case of a successful response, ReadFromJsonAsync can be used on the response´s Content property to read the data.
        //   - 400 BadRequest: return a friendly message like "Felaktiga parametrar".
        //   - 404 NotFound: return "Inga resultat hittades".
        //   - Other status codes: return a generic error.

        // Step 3: return a ApiResult<PlacementSearchResponse> with data or an error.

        // TODO: Replace the exception and return a friendly error if the PlacementServiceAPI cannot be reached.
        throw new NotImplementedException();
    }

    // offset is included for API consistency with SearchAsync but summary always uses offset=0 so the grouping reflects the full result set - not a single page.
    public async Task<ApiResult<PlacementSummaryResponse>> SummaryAsync(
        string query,
        string? region,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        // TODO: Follow the same pattern as in SearchAsync but call the summary endpoint instead

        // TODO: Replace the exception and return a ApiResult<PlacementSummaryResponse> with data or with an error.
        throw new NotImplementedException();
    }
}
