using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Helpers;
using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Services;

public class MovieUpsertService
{
    private readonly AppDbContext _dbContext;

    public MovieUpsertService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> UpsertImportedAsync(Movie incoming, CancellationToken cancellationToken = default)
    {
        var existing = await FindExistingAsync(incoming.Title, incoming.Year, incoming.MediaType, cancellationToken);
        if (existing is null)
        {
            incoming.NormalizedTitle = TitleNormalizer.Normalize(incoming.Title);
            incoming.CreatedUtc = DateTime.UtcNow;
            incoming.UpdatedUtc = DateTime.UtcNow;

            _dbContext.Movies.Add(incoming);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        MergeImportedIntoExisting(existing, incoming);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return false;
    }

    public async Task UpdateManualAsync(Movie existing, Movie incoming, CancellationToken cancellationToken = default)
    {
        existing.Title = incoming.Title.Trim();
        existing.OriginalTitle = NullIfWhiteSpace(incoming.OriginalTitle);
        existing.Year = incoming.Year;
        existing.Category = NullIfWhiteSpace(incoming.Category);
        existing.GenresCsv = NullIfWhiteSpace(incoming.GenresCsv);
        existing.MediaType = incoming.MediaType;
        existing.ImdbRating = incoming.ImdbRating;
        existing.ImdbVotes = incoming.ImdbVotes;
        existing.Metascore = incoming.Metascore;
        existing.RuntimeMinutes = incoming.RuntimeMinutes;
        existing.ReleasedOn = incoming.ReleasedOn;
        existing.Country = NullIfWhiteSpace(incoming.Country);
        existing.Language = NullIfWhiteSpace(incoming.Language);
        existing.Director = NullIfWhiteSpace(incoming.Director);
        existing.Writer = NullIfWhiteSpace(incoming.Writer);
        existing.Actors = NullIfWhiteSpace(incoming.Actors);
        existing.PosterUrl = NullIfWhiteSpace(incoming.PosterUrl);
        existing.Overview = NullIfWhiteSpace(incoming.Overview);
        existing.OmdbType = NullIfWhiteSpace(incoming.OmdbType);
        existing.OmdbRatingsJson = NullIfWhiteSpace(incoming.OmdbRatingsJson);
        existing.Notes = NullIfWhiteSpace(incoming.Notes);
        existing.UserRating = incoming.UserRating;
        existing.UserGrade = incoming.UserGrade;
        existing.PrimaryVerdict = incoming.PrimaryVerdict;
        existing.ReasonTagsCsv = NormalizeCsv(incoming.ReasonTagsCsv, RecommendationViewHelper.MaxReasonTags);
        existing.NormalizedTagsCsv = NormalizeCsv(incoming.NormalizedTagsCsv ?? incoming.ReasonTagsCsv, RecommendationViewHelper.MaxReasonTags);
        existing.NeedsTagReview = incoming.NeedsTagReview;
        existing.TagsCsv = NullIfWhiteSpace(incoming.TagsCsv);
        existing.TmdbKeywordsCsv = NullIfWhiteSpace(incoming.TmdbKeywordsCsv);
        existing.SimilarTitlesJson = NullIfWhiteSpace(incoming.SimilarTitlesJson);
        existing.ExternalRatingsJson = NullIfWhiteSpace(incoming.ExternalRatingsJson);
        existing.TmdbId = NullIfWhiteSpace(incoming.TmdbId);
        existing.WatchedStatus = incoming.WatchedStatus;
        existing.ExternalSource = NullIfWhiteSpace(incoming.ExternalSource);
        existing.ExternalId = NullIfWhiteSpace(incoming.ExternalId);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool Created, Movie Movie)> ApplyCsvDeltaAsync(CsvMovieDeltaRecord record, CancellationToken cancellationToken = default)
    {
        var existing = await FindExistingAsync(record.Title, record.Year, MediaType.Movie, cancellationToken)
            ?? await FindExistingAsync(record.Title, record.Year, MediaType.Series, cancellationToken);

        if (existing is null)
        {
            existing = new Movie
            {
                Title = record.Title.Trim(),
                Year = record.Year,
                MediaType = MediaType.Movie,
                WatchedStatus = WatchedStatus.Watched
            };
            _dbContext.Movies.Add(existing);
        }

        ApplyDeltaToMovie(existing, record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (existing.CreatedUtc == existing.UpdatedUtc, existing);
    }

    private async Task<Movie?> FindExistingAsync(string title, int? year, MediaType mediaType, CancellationToken cancellationToken)
    {
        var normalizedTitle = TitleNormalizer.Normalize(title);

        var candidates = await _dbContext.Movies
            .Where(x => x.NormalizedTitle == normalizedTitle && x.MediaType == mediaType)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        if (year.HasValue)
        {
            var exactYear = candidates.FirstOrDefault(x => x.Year == year);
            if (exactYear is not null)
            {
                return exactYear;
            }
        }

        return candidates[0];
    }

    private static void MergeImportedIntoExisting(Movie existing, Movie incoming)
    {
        existing.OriginalTitle = PreferIncoming(existing.OriginalTitle, incoming.OriginalTitle);
        existing.Year ??= incoming.Year;
        existing.Category = PreferIncoming(existing.Category, incoming.Category);
        existing.GenresCsv = PreferIncoming(existing.GenresCsv, incoming.GenresCsv);
        existing.ImdbRating ??= incoming.ImdbRating;
        existing.ImdbVotes ??= incoming.ImdbVotes;
        existing.Metascore ??= incoming.Metascore;
        existing.RuntimeMinutes ??= incoming.RuntimeMinutes;
        existing.ReleasedOn ??= incoming.ReleasedOn;
        existing.Country = PreferIncoming(existing.Country, incoming.Country);
        existing.Language = PreferIncoming(existing.Language, incoming.Language);
        existing.Director = PreferIncoming(existing.Director, incoming.Director);
        existing.Writer = PreferIncoming(existing.Writer, incoming.Writer);
        existing.Actors = PreferIncoming(existing.Actors, incoming.Actors);
        existing.PosterUrl = PreferIncoming(existing.PosterUrl, incoming.PosterUrl);
        existing.Overview = PreferIncoming(existing.Overview, incoming.Overview);
        existing.OmdbType = PreferIncoming(existing.OmdbType, incoming.OmdbType);
        existing.OmdbRatingsJson = PreferIncoming(existing.OmdbRatingsJson, incoming.OmdbRatingsJson);
        existing.ExternalId = PreferIncoming(existing.ExternalId, incoming.ExternalId);
        existing.ExternalSource = PreferIncoming(existing.ExternalSource, incoming.ExternalSource);

        if (existing.WatchedStatus == WatchedStatus.Unknown && incoming.WatchedStatus != WatchedStatus.Unknown)
        {
            existing.WatchedStatus = incoming.WatchedStatus;
        }

        existing.UserRating ??= incoming.UserRating;
        existing.UserGrade ??= incoming.UserGrade;
        existing.PrimaryVerdict ??= incoming.PrimaryVerdict;
        existing.ReasonTagsCsv = MergeCsv(existing.ReasonTagsCsv, incoming.ReasonTagsCsv, RecommendationViewHelper.MaxReasonTags);
        existing.NormalizedTagsCsv = MergeCsv(existing.NormalizedTagsCsv, incoming.NormalizedTagsCsv ?? incoming.ReasonTagsCsv, RecommendationViewHelper.MaxReasonTags);
        existing.NeedsTagReview = existing.NeedsTagReview || incoming.NeedsTagReview;
        existing.TmdbKeywordsCsv = PreferIncoming(existing.TmdbKeywordsCsv, incoming.TmdbKeywordsCsv);
        existing.SimilarTitlesJson = PreferIncoming(existing.SimilarTitlesJson, incoming.SimilarTitlesJson);
        existing.ExternalRatingsJson = PreferIncoming(existing.ExternalRatingsJson, incoming.ExternalRatingsJson);
        existing.TmdbId = PreferIncoming(existing.TmdbId, incoming.TmdbId);

        if (string.IsNullOrWhiteSpace(existing.Notes) && !string.IsNullOrWhiteSpace(incoming.Notes))
        {
            existing.Notes = incoming.Notes;
        }

        existing.TagsCsv = MergeCsv(existing.TagsCsv, incoming.TagsCsv);
    }

    private static void ApplyDeltaToMovie(Movie movie, CsvMovieDeltaRecord record)
    {
        var grade = RecommendationViewHelper.ParseGrade(record.UserGrade)
            ?? RecommendationViewHelper.MapScoreToGrade(record.LegacyUserScore)
            ?? movie.UserGrade;

        movie.Title = movie.Title.Trim();
        movie.Year ??= record.Year;
        movie.WatchedStatus = WatchedStatus.Watched;
        movie.UserGrade = grade;
        movie.PrimaryVerdict = RecommendationViewHelper.MapGradeToVerdict(grade);
        movie.UserRating ??= record.LegacyUserScore ?? (grade.HasValue ? RecommendationViewHelper.MapGradeToUserRating(grade.Value) : null);
        movie.Notes = AppendNote(movie.Notes, record.Notes);
        movie.ExternalSource = NullIfWhiteSpace(record.Source) ?? movie.ExternalSource;

        var reviewStatus = record.ReviewStatus?.Trim();
        if (string.Equals(reviewStatus, "NeedsTagReview", StringComparison.OrdinalIgnoreCase))
        {
            movie.NeedsTagReview = true;
            movie.ReasonTagsCsv = null;
            movie.NormalizedTagsCsv = null;
            return;
        }

        var normalizedTags = RecommendationViewHelper.NormalizeImportedTags(record.NewTagsCsv);
        movie.ReasonTagsCsv = RecommendationViewHelper.JoinCsv(normalizedTags);
        movie.NormalizedTagsCsv = movie.ReasonTagsCsv;
        movie.NeedsTagReview = normalizedTags.Count == 0;
    }

    private static string? AppendNote(string? existing, string? incoming)
    {
        incoming = NullIfWhiteSpace(incoming);
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return NullIfWhiteSpace(existing);
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return incoming;
        }

        return existing.Contains(incoming, StringComparison.OrdinalIgnoreCase)
            ? existing
            : $"{existing}{Environment.NewLine}{incoming}";
    }

    private static string? PreferIncoming(string? existing, string? incoming)
    {
        return string.IsNullOrWhiteSpace(incoming)
            ? NullIfWhiteSpace(existing)
            : NullIfWhiteSpace(incoming);
    }

    private static string? MergeCsv(string? left, string? right, int? take = null)
    {
        var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in RecommendationCatalog.SplitCsv(left))
        {
            allTags.Add(tag);
        }

        foreach (var tag in RecommendationCatalog.SplitCsv(right))
        {
            allTags.Add(tag);
        }

        var values = allTags.OrderBy(x => x).ToList();
        if (take.HasValue)
        {
            values = values.Take(take.Value).ToList();
        }

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string? NormalizeCsv(string? value, int? take = null)
    {
        var normalized = RecommendationCatalog.SplitCsv(value).Distinct(StringComparer.OrdinalIgnoreCase);
        if (take.HasValue)
        {
            normalized = normalized.Take(take.Value);
        }

        var values = normalized.ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
