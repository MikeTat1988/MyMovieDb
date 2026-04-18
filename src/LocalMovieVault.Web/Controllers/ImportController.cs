using LocalMovieVault.Web.Contracts;
using LocalMovieVault.Web.Data;
using LocalMovieVault.Web.Services;
using LocalMovieVault.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LocalMovieVault.Web.Controllers;

public class ImportController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly DocxMovieImportService _docxMovieImportService;
    private readonly JsonMovieImportService _jsonMovieImportService;
    private readonly CsvMovieDeltaImportService _csvMovieDeltaImportService;
    private readonly MovieUpsertService _movieUpsertService;
    private readonly PersonalMatchService _personalMatchService;
    private readonly AppEventLogService _appEventLogService;

    public ImportController(
        AppDbContext dbContext,
        DocxMovieImportService docxMovieImportService,
        JsonMovieImportService jsonMovieImportService,
        CsvMovieDeltaImportService csvMovieDeltaImportService,
        MovieUpsertService movieUpsertService,
        PersonalMatchService personalMatchService,
        AppEventLogService? appEventLogService = null)
    {
        _dbContext = dbContext;
        _docxMovieImportService = docxMovieImportService;
        _jsonMovieImportService = jsonMovieImportService;
        _csvMovieDeltaImportService = csvMovieDeltaImportService;
        _movieUpsertService = movieUpsertService;
        _personalMatchService = personalMatchService;
        _appEventLogService = appEventLogService ?? new AppEventLogService(dbContext);
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(new ImportPageViewModel
        {
            ExistingCount = await _dbContext.Movies.CountAsync(cancellationToken),
            StatusMessage = TempData["ImportStatus"] as string,
            ErrorMessage = TempData["ImportError"] as string
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? uploadFile, CancellationToken cancellationToken)
    {
        if (uploadFile is null || uploadFile.Length == 0)
        {
            TempData["ImportError"] = "Choose a .docx, .json, or .csv file.";
            await _appEventLogService.WriteImportEventAsync(
                "Import.Upload",
                "Rejected",
                "Import rejected because no file was provided.",
                Request.Path.ToString(),
                new { FileName = uploadFile?.FileName, Length = uploadFile?.Length ?? 0L },
                cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(uploadFile.FileName);
        await using var stream = uploadFile.OpenReadStream();

        switch (extension.ToLowerInvariant())
        {
            case ".docx":
            {
                var records = await _docxMovieImportService.ParseAsync(stream, cancellationToken);
                await ApplySeedRecordsAsync(records, cancellationToken);
                TempData["ImportStatus"] = $"Import complete: processed {records.Count} DOCX rows.";
                await _appEventLogService.WriteImportEventAsync(
                    "Import.Upload",
                    "Success",
                    $"Imported {records.Count} DOCX rows from '{uploadFile.FileName}'.",
                    Request.Path.ToString(),
                    new { uploadFile.FileName, uploadFile.Length, Extension = extension, Records = records.Count },
                    cancellationToken);
                break;
            }
            case ".json":
            {
                var records = await _jsonMovieImportService.ParseAsync(stream, cancellationToken);
                await ApplySeedRecordsAsync(records, cancellationToken);
                TempData["ImportStatus"] = $"Import complete: processed {records.Count} JSON rows.";
                await _appEventLogService.WriteImportEventAsync(
                    "Import.Upload",
                    "Success",
                    $"Imported {records.Count} JSON rows from '{uploadFile.FileName}'.",
                    Request.Path.ToString(),
                    new { uploadFile.FileName, uploadFile.Length, Extension = extension, Records = records.Count },
                    cancellationToken);
                break;
            }
            case ".csv":
            {
                var rows = await _csvMovieDeltaImportService.ParseAsync(stream, cancellationToken);
                var summary = new ImportSummary();
                foreach (var row in rows)
                {
                    var result = await _movieUpsertService.ApplyCsvDeltaAsync(row, cancellationToken);
                    if (result.Created)
                    {
                        summary.Added++;
                    }
                    else
                    {
                        summary.Updated++;
                    }
                }

                await _personalMatchService.RecalculateAsync(cancellationToken);
                TempData["ImportStatus"] = $"CSV delta applied: added {summary.Added}, updated {summary.Updated}, processed {rows.Count}.";
                await _appEventLogService.WriteImportEventAsync(
                    "Import.Upload",
                    "Success",
                    $"Applied CSV delta from '{uploadFile.FileName}'.",
                    Request.Path.ToString(),
                    new
                    {
                        uploadFile.FileName,
                        uploadFile.Length,
                        Extension = extension,
                        Rows = rows.Count,
                        summary.Added,
                        summary.Updated
                    },
                    cancellationToken);
                break;
            }
            default:
                TempData["ImportError"] = "Supported formats: .docx, .json, .csv.";
                await _appEventLogService.WriteImportEventAsync(
                    "Import.Upload",
                    "Rejected",
                    $"Rejected import '{uploadFile.FileName}' because the format is unsupported.",
                    Request.Path.ToString(),
                    new { uploadFile.FileName, uploadFile.Length, Extension = extension },
                    cancellationToken);
                break;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ApplySeedRecordsAsync(List<SeedMovieRecord> records, CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            await _movieUpsertService.UpsertImportedAsync(record.ToEntity(), cancellationToken);
        }

        await _personalMatchService.RecalculateAsync(cancellationToken);
    }
}
