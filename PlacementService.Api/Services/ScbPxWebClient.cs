using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PlacementService.Api.Models;
using PlacementService.Api.Options;

namespace PlacementService.Api.Services;

public sealed class ScbPxWebClient
{
    private const string MetadataCacheKey = "scb:pxweb:metadata";

    private readonly HttpClient _httpClient;
    private readonly ScbPxWebOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ScbPxWebClient> _logger;

    public ScbPxWebClient(
        HttpClient httpClient,
        IOptions<ScbPxWebOptions> options,
        IMemoryCache cache,
        ILogger<ScbPxWebClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SalaryInfo?> GetSalaryAsync(string ssyk, int? year, CancellationToken cancellationToken)
    {

        var normalizedSsyk = NormalizeSsyk(ssyk, _options.NormalizeSsykTo3Digits);
        if (string.IsNullOrWhiteSpace(normalizedSsyk))
        {
            return null;
        }

        var metadata = await GetMetadataAsync(cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var targetYear = ResolveYear(year, metadata.TimeVariable?.Values, _options.DefaultYear);
        if (string.IsNullOrWhiteSpace(targetYear))
        {
            return null;
        }

        foreach (var ssykCandidate in BuildSsykCandidates(normalizedSsyk))
        {
            var payload = BuildRequestPayload(metadata, ssykCandidate, targetYear);
            if (payload is null)
            {
                continue;
            }

            var requestBody = JsonSerializer.Serialize(payload);
            _logger.LogDebug("SCB payload: {Payload}", requestBody);

            var salary = await QuerySalaryAsync(payload, cancellationToken);
            if (salary is not null)
            {
                var salaryYear = int.TryParse(targetYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear)
                    ? parsedYear
                    : _options.DefaultYear;

                return new SalaryInfo(ssykCandidate, salaryYear, salary, _options.SourceName);
            }
        }

        return null;
    }

    private async Task<ScbMetadata?> GetMetadataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(MetadataCacheKey, out ScbMetadata? cached) && cached is not null)
        {
            return cached;
        }

        var metadataUrl = $"{_options.TableUrl}/metadata?lang=sv";

        using var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("SCB metadata returned {StatusCode}: {Payload}", response.StatusCode, payload);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var metadata = ParseMetadata(document.RootElement);
        if (metadata is null)
        {
            return null;
        }

        _cache.Set(MetadataCacheKey, metadata, TimeSpan.FromHours(6));
        return metadata;
    }

    private async Task<decimal?> QuerySalaryAsync(object payload, CancellationToken cancellationToken)
    {
        // Be explicit about response format so parsing is predictable across PxWeb v2 installations.
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.TableUrl}/data?lang=sv&outputFormat=json-stat2")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("SCB query failed with {StatusCode}: {Payload}", response.StatusCode, payloadText);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseSalary(document.RootElement);
    }

    private static ScbMetadata? ParseMetadata(JsonElement root)
    {
        if (root.TryGetProperty("variables", out var variablesElement) && variablesElement.ValueKind == JsonValueKind.Array)
        {
            return ParseLegacyMetadata(variablesElement);
        }

        if (root.TryGetProperty("id", out var idsElement)
            && idsElement.ValueKind == JsonValueKind.Array
            && root.TryGetProperty("dimension", out var dimensionsElement)
            && dimensionsElement.ValueKind == JsonValueKind.Object)
        {
            return ParseJsonStatMetadata(idsElement, dimensionsElement);
        }

        return null;
    }

    private static ScbMetadata? ParseLegacyMetadata(JsonElement variablesElement)
    {
        var variables = new List<ScbVariable>();
        foreach (var variableElement in variablesElement.EnumerateArray())
        {
            if (variableElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var code = GetString(variableElement, "id") ?? GetString(variableElement, "code");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var text  = GetString(variableElement, "label") ?? GetString(variableElement, "text") ?? string.Empty;
            var values = ReadValueCodes(variableElement, "values");

            variables.Add(new ScbVariable(code, text, values));
        }

        if (variables.Count == 0)
        {
            return null;
        }

        return BuildMetadataFromVariables(variables);
    }

    private static ScbMetadata? ParseJsonStatMetadata(JsonElement idsElement, JsonElement dimensionsElement)
    {
        var variables = new List<ScbVariable>();

        foreach (var idElement in idsElement.EnumerateArray())
        {
            if (idElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var code = idElement.GetString();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (!dimensionsElement.TryGetProperty(code, out var dimensionElement)
                || dimensionElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var text = GetString(dimensionElement, "label") ?? code;
            var values = ReadJsonStatValueCodes(dimensionElement);
            variables.Add(new ScbVariable(code, text, values));
        }

        if (variables.Count == 0)
        {
            return null;
        }

        return BuildMetadataFromVariables(variables);
    }

    private static ScbMetadata BuildMetadataFromVariables(IReadOnlyList<ScbVariable> variables)
    {
        var occupation = variables.FirstOrDefault(v => v.Code.Contains("yrke", StringComparison.OrdinalIgnoreCase));
        occupation ??= variables.FirstOrDefault(v => v.Text.Contains("yrke", StringComparison.OrdinalIgnoreCase));
        occupation ??= variables.FirstOrDefault(v => v.Code.Contains("ssyk", StringComparison.OrdinalIgnoreCase));
        occupation ??= variables.FirstOrDefault(v => v.Text.Contains("ssyk", StringComparison.OrdinalIgnoreCase));

        var time = variables.FirstOrDefault(v => string.Equals(v.Code, "Tid", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Code.Contains("tid", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Text.Contains("tid", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Code.Contains("time", StringComparison.OrdinalIgnoreCase));

        var gender = variables.FirstOrDefault(v => v.Code.Contains("kon", StringComparison.OrdinalIgnoreCase)
            || v.Code.Contains("kön", StringComparison.OrdinalIgnoreCase)
            || v.Text.Contains("kon", StringComparison.OrdinalIgnoreCase)
            || v.Text.Contains("kön", StringComparison.OrdinalIgnoreCase));

        var sector = variables.FirstOrDefault(v => v.Code.Contains("sektor", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Text.Contains("sektor", StringComparison.OrdinalIgnoreCase));

        var contents = variables.FirstOrDefault(v => string.Equals(v.Code, "ContentsCode", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Code.Contains("contents", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Text.Contains("tabellinnehåll", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Text.Contains("innehåll", StringComparison.OrdinalIgnoreCase))
            ?? variables.FirstOrDefault(v => v.Text.Contains("content", StringComparison.OrdinalIgnoreCase));

        return new ScbMetadata(variables, occupation, time, gender, sector, contents);
    }

    private static IReadOnlyList<string> ReadValueCodes(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var codes = new List<string>();
        foreach (var item in valuesElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String) codes.Add(item.GetString()!);
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("code", out var codeElem) && codeElem.ValueKind == JsonValueKind.String)
                codes.Add(codeElem.GetString()!);
        }
        return codes;
    }

    private object? BuildRequestPayload(ScbMetadata metadata, string ssykCandidate, string targetYear)
    {
        if (metadata.OccupationVariable is null || metadata.TimeVariable is null)
            return null;

        var selections = new List<object>();
        foreach (var variable in metadata.Variables)
        {
            string? selectedValue;

            if (variable.Code == metadata.OccupationVariable.Code)
            {
                selectedValue = SelectMatching(variable.Values, ssykCandidate);
            }
            else if (variable.Code == metadata.TimeVariable.Code)
            {
                selectedValue = SelectMatching(variable.Values, targetYear);
            }
            else if (metadata.GenderVariable is not null && variable.Code == metadata.GenderVariable.Code)
            {
                selectedValue = SelectConfiguredOrFirst(variable.Values, _options.GenderCode);
            }
            else if (metadata.SectorVariable is not null && variable.Code == metadata.SectorVariable.Code)
            {
                selectedValue = SelectConfiguredOrFirst(variable.Values, _options.SectorCode);
            }
            else if (metadata.ContentsVariable is not null && variable.Code == metadata.ContentsVariable.Code)
            {
                selectedValue = SelectConfiguredOrFirst(variable.Values, _options.ContentCode);
            }
            else
            {
                selectedValue = variable.Values.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                return null;
            }

            selections.Add(new
            {
                variableCode = variable.Code,
                // Different PxWeb v2 deployments have used both valueCodes and values for whatever reason.
                // Sending both to stay compatible.
                valueCodes = new[] { selectedValue },
                values = new[] { selectedValue }
            });
        }

        return new { selection = selections };
    }

    private static IEnumerable<string> BuildSsykCandidates(string normalizedSsyk)
    {
        yield return normalizedSsyk;

        if (normalizedSsyk.Length >= 4)
        {
            yield return normalizedSsyk[..3];
        }
    }

    private static string? ResolveYear(int? requestedYear, IReadOnlyList<string>? availableYears, int fallbackYear)
    {
        if (availableYears is null || availableYears.Count == 0)
        {
            return requestedYear?.ToString(CultureInfo.InvariantCulture) ?? fallbackYear.ToString(CultureInfo.InvariantCulture);
        }

        if (requestedYear is not null)
        {
            var requested = requestedYear.Value.ToString(CultureInfo.InvariantCulture);
            if (availableYears.Contains(requested, StringComparer.OrdinalIgnoreCase))
            {
                return requested;
            }
        }

        var numericYears = availableYears
            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        if (numericYears.Count > 0)
        {
            return numericYears.Max().ToString(CultureInfo.InvariantCulture);
        }

        return availableYears[^1];
    }

    private static string? SelectMatching(IReadOnlyList<string> values, string target)
        => values.FirstOrDefault(value => string.Equals(value, target, StringComparison.OrdinalIgnoreCase));

    private static string? SelectConfiguredOrFirst(IReadOnlyList<string> values, string configured)
        => values.FirstOrDefault(value => string.Equals(value, configured, StringComparison.OrdinalIgnoreCase))
           ?? values.FirstOrDefault();

    private static decimal? ParseSalary(JsonElement root)
    {
        if (root.TryGetProperty("value", out var jsonStatValueElement))
        {
            var parsed = ParseJsonStatValue(jsonStatValueElement);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = dataElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!first.TryGetProperty("values", out var valuesElement) || valuesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var valueElement = valuesElement.EnumerateArray().FirstOrDefault();
        if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out var number))
        {
            return number;
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var text = valueElement.GetString();
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static decimal? ParseJsonStatValue(JsonElement valueElement)
    {
        if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDecimal(out var number))
        {
            return number;
        }

        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var textValue = valueElement.GetString();
            if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        if (valueElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in valueElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetDecimal(out var value))
            {
                return value;
            }

            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadJsonStatValueCodes(JsonElement dimensionElement)
    {
        if (!dimensionElement.TryGetProperty("category", out var categoryElement)
            || categoryElement.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        if (!categoryElement.TryGetProperty("index", out var indexElement))
        {
            return Array.Empty<string>();
        }

        if (indexElement.ValueKind == JsonValueKind.Array)
        {
            return indexElement.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!)
                .ToList();
        }

        if (indexElement.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        var withSortOrder = new List<(string Code, int? Position)>();
        foreach (var property in indexElement.EnumerateObject())
        {
            int? position = null;
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var parsed))
            {
                position = parsed;
            }
            else if (property.Value.ValueKind == JsonValueKind.String
                     && int.TryParse(property.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFromText))
            {
                position = parsedFromText;
            }

            withSortOrder.Add((property.Name, position));
        }

        return withSortOrder
            .OrderBy(item => item.Position ?? int.MaxValue)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .Select(item => item.Code)
            .ToList();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return arrayElement.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToList();
    }

    private static string? NormalizeSsyk(string? ssyk, bool normalizeTo3Digits)
    {
        if (string.IsNullOrWhiteSpace(ssyk))
        {
            return null;
        }

        var digits = new string(ssyk.Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (normalizeTo3Digits && digits.Length >= 3)
        {
            return digits[..3];
        }

        return digits.Length > 4 ? digits[..4] : digits;
    }

    private sealed record ScbVariable(string Code, string Text, IReadOnlyList<string> Values);

    private sealed record ScbMetadata(
        IReadOnlyList<ScbVariable> Variables,
        ScbVariable? OccupationVariable,
        ScbVariable? TimeVariable,
        ScbVariable? GenderVariable,
        ScbVariable? SectorVariable,
        ScbVariable? ContentsVariable);
}
