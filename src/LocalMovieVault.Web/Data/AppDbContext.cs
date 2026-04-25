using LocalMovieVault.Web.Models;
using LocalMovieVault.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<AppEvent> AppEvents => Set<AppEvent>();

    public override int SaveChanges()
    {
        NormalizeMovies();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeMovies();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasIndex(x => x.NormalizedTitle);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.MediaType);
            entity.HasIndex(x => x.PersonalMatchScore);
            entity.HasIndex(x => x.PredictedScore);
            entity.HasIndex(x => x.UserGrade);
            entity.HasIndex(x => x.PrimaryVerdict);
            entity.HasIndex(x => x.IsDismissed);
            entity.HasIndex(x => x.NeedsTagReview);
            entity.HasIndex(x => x.IsWowPick);
            entity.Property(x => x.Overview).HasColumnType("TEXT");
            entity.Property(x => x.Notes).HasColumnType("TEXT");
            entity.Property(x => x.Director).HasColumnType("TEXT");
            entity.Property(x => x.Writer).HasColumnType("TEXT");
            entity.Property(x => x.Actors).HasColumnType("TEXT");
            entity.Property(x => x.OmdbRatingsJson).HasColumnType("TEXT");
            entity.Property(x => x.ExternalRatingsJson).HasColumnType("TEXT");
            entity.Property(x => x.SimilarTitlesJson).HasColumnType("TEXT");
            entity.Property(x => x.RecommendationContextJson).HasColumnType("TEXT");
            entity.Property(x => x.DismissedReasonTagsCsv).HasColumnType("TEXT");
        });

        modelBuilder.Entity<AppEvent>(entity =>
        {
            entity.HasIndex(x => x.OccurredUtc);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.Scope);
            entity.Property(x => x.DetailsJson).HasColumnType("TEXT");
        });
    }

    private void NormalizeMovies()
    {
        var trackedEntries = ChangeTracker
            .Entries<Movie>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in trackedEntries)
        {
            entry.Entity.Title = entry.Entity.Title.Trim();
            entry.Entity.NormalizedTitle = TitleNormalizer.Normalize(entry.Entity.Title);

            if (entry.State == EntityState.Added && entry.Entity.CreatedUtc == default)
            {
                entry.Entity.CreatedUtc = DateTime.UtcNow;
            }

            entry.Entity.UpdatedUtc = DateTime.UtcNow;
        }
    }
}
