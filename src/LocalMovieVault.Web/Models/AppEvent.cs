using System.ComponentModel.DataAnnotations;

namespace LocalMovieVault.Web.Models;

public sealed class AppEvent
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Scope { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string EventType { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Outcome { get; set; } = string.Empty;

    [Required, MaxLength(240)]
    public string Summary { get; set; } = string.Empty;

    public string? DetailsJson { get; set; }

    public int? RelatedMovieId { get; set; }

    [MaxLength(200)]
    public string? RelatedMovieTitle { get; set; }

    [MaxLength(260)]
    public string? RequestPath { get; set; }

    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}
