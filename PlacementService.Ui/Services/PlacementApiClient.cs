using System.Net;
using Microsoft.AspNetCore.WebUtilities;
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
        // Step 1: build a dictionary with query, limit and offset, plus region if provided to act as the parameters for the API-query. 
        // Build a url variable to include the API endpoint and the query parameters
        // HINT: Use QueryHelpers.AddQueryString to append parameters to the base path
        
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        
        parameters = new Dictionary<string, string>
        {
            ["q"] = query,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString()
        };

        if (!string.IsNullOrWhiteSpace(region))
        {
            parameters["region"] = region;
        }
        
        string url = QueryHelpers.AddQueryString("http://localhost:5005/api/placements/search", parameters);
        Console.WriteLine(url);


        // Step 2: call _httpClient.GetAsync() with the full URL and inspect the response.StatusCode. You should then handle the following responses correctly:
        //   - 200 OK: deserialize JSON into PlacementSearchResponse.
        // HINT: In case of a successful response, ReadFromJsonAsync can be used on the response´s Content property to read the data.
        //   - 400 BadRequest: return a friendly message like "Felaktiga parametrar".
        //   - 404 NotFound: return "Inga resultat hittades".
        //   - Other status codes: return a generic error.


        var response = await _httpClient.GetAsync(url, cancellationToken: cancellationToken);

        // Step 3: return a ApiResult<PlacementSearchResponse> with data or an error.


        try
        {
            switch (response.StatusCode)
            {
            case HttpStatusCode.OK:
                {
                    Console.WriteLine("API call successful");
                   var result = await response.Content.ReadFromJsonAsync<PlacementSearchResponse>(cancellationToken: cancellationToken);
                   return new ApiResult<PlacementSearchResponse>(result, null);
                }

            case HttpStatusCode.BadRequest:
                {
                    Console.WriteLine("API call failed with BadRequest");
                    return new ApiResult<PlacementSearchResponse>(null, "Felaktiga parametrar");
                }
                
            case HttpStatusCode.NotFound:
                {
                    Console.WriteLine("API call failed with NotFound");
                    return new ApiResult<PlacementSearchResponse>(null, "Inga resultat hittades");
                }

            default:
                {
                    Console.WriteLine($"API call failed with status code: {response.StatusCode}");
                    return new ApiResult<PlacementSearchResponse>(null, "Ett fel inträffade vid anrop av API:et");
                }
            }

        } catch (HttpRequestException)
        {
            return new ApiResult<PlacementSearchResponse>(null, "API:et kan inte nås för tillfället");
        }
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

       Dictionary<string, string> parameters = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(region))
        {
            parameters = new Dictionary<string, string>
            {
                ["query"] = query,
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString()
            };
        }


        parameters = new Dictionary<string, string>
        {
            ["query"] = query,
            ["region"] = region!,
            ["limit"] = limit.ToString(),
            ["offset"] = offset.ToString()
        };
        
        string url = QueryHelpers.AddQueryString("placements/search", parameters);


        var response = await _httpClient.GetAsync(url, cancellationToken: cancellationToken);

        try
        {
            switch (response.StatusCode)
            {
            case HttpStatusCode.OK:
                {
                   var result = await response.Content.ReadFromJsonAsync<PlacementSummaryResponse>(cancellationToken: cancellationToken);
                   return new ApiResult<PlacementSummaryResponse>(result, null);
                }

            case HttpStatusCode.BadRequest:
                {
                    return new ApiResult<PlacementSummaryResponse>(null, "Felaktiga parametrar");
                }
                
            case HttpStatusCode.NotFound:
                {
                    return new ApiResult<PlacementSummaryResponse>(null, "Inga resultat hittades");
                }

            default:
                {
                    return new ApiResult<PlacementSummaryResponse>(null, "Ett fel inträffade vid anrop av API:et");
                }
            }

        } catch (HttpRequestException)
        {
            return new ApiResult<PlacementSummaryResponse>(null, "API:et kan inte nås för tillfället");
        }
    }
}
