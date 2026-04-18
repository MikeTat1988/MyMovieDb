using System.Text.Json;
using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Data;

namespace LocalMovieVault.Web.Services;

public class SeedDataService
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly MovieUpsertService _upsertService;
    private readonly AppStorageOptions _storageOptions;

    public SeedDataService(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        MovieUpsertService upsertService,
        AppStorageOptions storageOptions)
    {
        _dbContext = dbContext;
        _environment = environment;
        _upsertService = upsertService;
        _storageOptions = storageOptions;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Movies.Any())
        {
            return;
        }

        var seedPath = _storageOptions.SeedPath;
        if (!File.Exists(seedPath))
        {
            var bundledSeedPath = Path.Combine(_environment.ContentRootPath, "App_Data", "Seed", "seed_movies.json");
            if (!File.Exists(bundledSeedPath))
            {
                bundledSeedPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "..", "source-data", "seed", "seed_movies.json"));
            }

            if (File.Exists(bundledSeedPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(seedPath)!);
                File.Copy(bundledSeedPath, seedPath, overwrite: true);
            }
        }

        if (!File.Exists(seedPath))
        {
            return;
        }

        await using var stream = File.OpenRead(seedPath);
        var records = await JsonSerializer.DeserializeAsync<List<SeedMovieRecord>>(stream, cancellationToken: cancellationToken)
                      ?? [];

        foreach (var record in records.Where(x => !string.IsNullOrWhiteSpace(x.Title)))
        {
            await _upsertService.UpsertImportedAsync(record.ToEntity(), cancellationToken);
        }
    }
}
