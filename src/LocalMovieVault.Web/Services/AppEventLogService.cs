using System.Text.Json;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services;

public sealed class AppEventLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly AppDbContext _dbContext;

    public AppEventLogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteMovieEventAsync(
        string eventType,
        string outcome,
        string summary,
        Movie? movie,
        string? requestPath = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(new AppEvent
        {
            Scope = "Movie",
            EventType = eventType,
            Outcome = outcome,
            Summary = summary,
            DetailsJson = SerializeDetails(details),
            RelatedMovieId = movie?.Id,
            RelatedMovieTitle = movie?.Title,
            RequestPath = requestPath
        }, cancellationToken);
    }

    public async Task WriteImportEventAsync(
        string eventType,
        string outcome,
        string summary,
        string? requestPath = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(new AppEvent
        {
            Scope = "Import",
            EventType = eventType,
            Outcome = outcome,
            Summary = summary,
            DetailsJson = SerializeDetails(details),
            RequestPath = requestPath
        }, cancellationToken);
    }

    public async Task WriteSettingsEventAsync(
        string eventType,
        string outcome,
        string summary,
        string? requestPath = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(new AppEvent
        {
            Scope = "Settings",
            EventType = eventType,
            Outcome = outcome,
            Summary = summary,
            DetailsJson = SerializeDetails(details),
            RequestPath = requestPath
        }, cancellationToken);
    }

    public Task<List<AppEvent>> GetRecentAsync(int take = 120, CancellationToken cancellationToken = default)
        => _dbContext.AppEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 250))
            .ToListAsync(cancellationToken);

    private async Task WriteAsync(AppEvent entry, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.AppEvents.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Logging should not block the user action it is describing.
        }
    }

    private static string? SerializeDetails(object? details)
        => details is null ? null : JsonSerializer.Serialize(details, JsonOptions);
}
