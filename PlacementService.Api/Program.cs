using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlacementService.Api.Models;
using PlacementService.Api.Options;
using PlacementService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JobSearchOptions>()
    .Bind(builder.Configuration.GetSection(JobSearchOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ScbPxWebOptions>()
    .Bind(builder.Configuration.GetSection(ScbPxWebOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<JobSearchClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<JobSearchOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});

builder.Services.AddHttpClient<ScbPxWebClient>();

builder.Services.AddScoped<PlacementServiceFacade>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/placements/search", async (
    [FromServices] PlacementServiceFacade facade,
    [FromQuery(Name = "q")] string? query,
    [FromQuery(Name = "region")] string? region,
    [FromQuery(Name = "limit")] int? limit,
    [FromQuery(Name = "offset")] int? offset,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { error = "Parameter 'q' is required." });
    }

    var resolvedLimit = limit ?? 10;
    if (resolvedLimit is < 1 or > 25)
    {
        return Results.BadRequest(new { error = "Parameter 'limit' must be between 1 and 25." });
    }

    var resolvedOffset = offset ?? 0;
    if (resolvedOffset < 0)
    {
        return Results.BadRequest(new { error = "Parameter 'offset' must be 0 or greater." });
    }

    try
    {
        var response = await facade.SearchAsync(query.Trim(), region, resolvedLimit, resolvedOffset, cancellationToken);
        return Results.Ok(response);
    }
    catch (HttpRequestException)
    {
        return Results.Problem("Upstream API failed.", statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("SearchPlacements")
.Produces<PlacementSearchResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status502BadGateway);

app.MapGet("/api/placements/summary", async (
    [FromServices] PlacementServiceFacade facade,
    [FromQuery(Name = "q")] string? query,
    [FromQuery(Name = "region")] string? region,
    [FromQuery(Name = "limit")] int? limit,
    [FromQuery(Name = "offset")] int? offset,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { error = "Parameter 'q' is required." });
    }

    var resolvedLimit = limit ?? 25;
    if (resolvedLimit is < 1 or > 100)
    {
        return Results.BadRequest(new { error = "Parameter 'limit' must be between 1 and 100." });
    }

    var resolvedOffset = offset ?? 0;
    if (resolvedOffset < 0)
    {
        return Results.BadRequest(new { error = "Parameter 'offset' must be 0 or greater." });
    }

    try
    {
        var response = await facade.GetSummaryAsync(query.Trim(), region, resolvedLimit, resolvedOffset, cancellationToken);
        return Results.Ok(response);
    }
    catch (HttpRequestException)
    {
        return Results.Problem("Upstream API failed.", statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithName("GetPlacementSummary")
.Produces<PlacementSummaryResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status502BadGateway);

app.MapGet("/api/placements/salary/{ssyk}", async (
    [FromServices] PlacementServiceFacade facade,
    [FromRoute] string ssyk,
    [FromQuery(Name = "year")] int? year,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(ssyk))
    {
        return Results.BadRequest(new { error = "Parameter 'ssyk' is required." });
    }

    if (year is not null && (year < 2000 || year > DateTime.UtcNow.Year + 1))
    {
        return Results.BadRequest(new { error = "Parameter 'year' is outside the supported range." });
    }

    var salary = await facade.GetSalaryAsync(ssyk, year, cancellationToken);
    if (salary is null)
    {
        return Results.NotFound(new { error = "Salary not found." });
    }

    return Results.Ok(salary);
})
.WithName("GetSalary")
.Produces<SalaryInfo>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status400BadRequest);

app.Run();
