namespace LocalMovieVault.Web.Services;

public sealed class AppStorageOptions
{
    public required string DataHomePath { get; init; }
    public required string DatabasePath { get; init; }
    public required string SettingsPath { get; init; }
    public required string SeedPath { get; init; }
}
