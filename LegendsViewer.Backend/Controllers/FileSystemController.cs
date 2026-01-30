using LegendsViewer.Backend.Contracts;
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends.Bookmarks;
using Microsoft.AspNetCore.Mvc;

namespace LegendsViewer.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileSystemController : ControllerBase
{
    private readonly string _dataDirectory;
    private readonly IConfiguration _configuration;
    private readonly IBookmarkService _bookmarkService;
    private readonly ILogger<FileSystemController> _logger;
    private const long MaxFileSize = 1024L * 1024L * 1024L; // 1 GB max file size

    public FileSystemController(IConfiguration configuration, IBookmarkService bookmarkService, ILogger<FileSystemController> logger)
    {
        _configuration = configuration;
        _bookmarkService = bookmarkService;
        _logger = logger;
        _dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        // Ensure directory exists
        Directory.CreateDirectory(_dataDirectory);
    }

    [HttpGet]
    [ProducesResponseType<FilesAndSubdirectoriesDto>(StatusCodes.Status200OK)]
    public ActionResult<FilesAndSubdirectoriesDto> Get()
    {
        // Only return files from data directory
        var files = Directory.GetFiles(_dataDirectory, $"*{BookmarkController.FileIdentifierLegendsXml}")
            .Select(f => Path.GetFileName(f) ?? "")
            .Order()
            .ToArray();

        return Ok(new FilesAndSubdirectoriesDto
        {
            CurrentDirectory = _dataDirectory,
            ParentDirectory = null, // No parent navigation
            Subdirectories = Array.Empty<string>(), // No subdirectory browsing
            Files = files
        });
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [RequestSizeLimit(MaxFileSize * 10)] // Allow up to 10 files (10 GB total)
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize * 10)]
    public async Task<ActionResult> UploadFile([FromForm] IFormFileCollection files)
    {
        // Check admin authorization
        if (!HttpContext.IsAdmin(_configuration))
        {
            _logger.LogWarning("Upload rejected: unauthorized (missing or invalid admin key)");
            return Unauthorized("Admin access required to upload files.");
        }

        if (files == null || files.Count == 0)
        {
            _logger.LogWarning("Upload rejected: no files provided");
            return BadRequest("No files provided.");
        }

        var totalSize = files.Sum(f => f?.Length ?? 0);
        _logger.LogInformation("Upload started. FileCount={FileCount}, TotalSizeBytes={TotalSizeBytes}, TotalSizeMB={TotalSizeMB:F1}",
            files.Count, totalSize, totalSize / (1024.0 * 1024.0));

        var uploadedFiles = new List<object>();
        var errors = new List<string>();

        // Validate file extension (only allow XML and related files)
        var allowedExtensions = new[] { ".xml", ".txt", ".bmp" };

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
            {
                var msg = $"File '{file?.FileName ?? "unknown"}' is empty.";
                errors.Add(msg);
                _logger.LogWarning("Upload validation failed: {Message}", msg);
                continue;
            }

            // Validate file size (1 GB max per file)
            if (file.Length > MaxFileSize)
            {
                var msg = $"File '{file.FileName}' exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)} MB.";
                errors.Add(msg);
                _logger.LogWarning("Upload validation failed: FileName={FileName}, SizeBytes={SizeBytes}", file.FileName, file.Length);
                continue;
            }

            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                var msg = $"File '{file.FileName}' type not allowed. Only {string.Join(", ", allowedExtensions)} files are permitted.";
                errors.Add(msg);
                _logger.LogWarning("Upload validation failed: {Message}", msg);
                continue;
            }

            // Validate filename (prevent path traversal)
            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Contains("..") ||
                fileName.Contains("/") ||
                fileName.Contains("\\"))
            {
                var msg = $"File '{file.FileName}' has an invalid name.";
                errors.Add(msg);
                _logger.LogWarning("Upload validation failed: {Message}", msg);
                continue;
            }

            try
            {
                var filePath = Path.Combine(_dataDirectory, fileName);

                // Check if file already exists
                if (System.IO.File.Exists(filePath))
                {
                    var msg = $"File '{fileName}' already exists. Delete it first or use a different name.";
                    errors.Add(msg);
                    _logger.LogWarning("Upload skipped (file exists): FileName={FileName}", fileName);
                    continue;
                }

                _logger.LogInformation("Uploading file. FileName={FileName}, SizeBytes={SizeBytes}, SizeMB={SizeMB:F1}",
                    fileName, file.Length, file.Length / (1024.0 * 1024.0));

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                uploadedFiles.Add(new { fileName, size = file.Length });
                _logger.LogInformation("Upload completed for file. FileName={FileName}, SizeBytes={SizeBytes}", fileName, file.Length);
            }
            catch (Exception ex)
            {
                var msg = $"Error uploading file '{file.FileName}': {ex.Message}";
                errors.Add(msg);
                _logger.LogError(ex, "Upload failed for file. FileName={FileName}, SizeBytes={SizeBytes}", file.FileName, file.Length);
            }
        }

        if (errors.Count > 0 && uploadedFiles.Count == 0)
        {
            _logger.LogWarning("Upload finished: all files failed. ErrorCount={ErrorCount}, Errors={Errors}", errors.Count, string.Join("; ", errors));
            return BadRequest(new { message = "All files failed to upload.", errors });
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Upload finished: partial success. UploadedCount={UploadedCount}, ErrorCount={ErrorCount}, Errors={Errors}",
                uploadedFiles.Count, errors.Count, string.Join("; ", errors));
            return Ok(new { message = "Some files uploaded successfully.", uploadedFiles, errors });
        }

        _logger.LogInformation("Upload finished: all files succeeded. FileCount={FileCount}, TotalSizeBytes={TotalSizeBytes}",
            uploadedFiles.Count, totalSize);
        return Ok(new { message = "All files uploaded successfully.", uploadedFiles });
    }

    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteFile(string fileName)
    {
        // Check admin authorization
        if (!HttpContext.IsAdmin(_configuration))
        {
            return Unauthorized("Admin access required to delete files.");
        }

        // Validate filename (prevent path traversal)
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains("..") ||
            fileName.Contains("/") ||
            fileName.Contains("\\"))
        {
            return BadRequest("Invalid file name.");
        }

        var filePath = Path.Combine(_dataDirectory, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound($"File '{fileName}' not found.");
        }

        try
        {
            // Extract regionId from filename to find related files
            string regionId = string.Empty;
            if (fileName.Contains(BookmarkController.FileIdentifierLegendsXml))
            {
                regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsXml, "");
            }
            else if (fileName.Contains(BookmarkController.FileIdentifierLegendsPlusXml))
            {
                regionId = fileName.Replace(BookmarkController.FileIdentifierLegendsPlusXml, "");
            }
            else
            {
                // If it's not a legends file, just delete the single file
                System.IO.File.Delete(filePath);
                return Ok(new { message = $"File '{fileName}' deleted successfully.", deletedFiles = new[] { fileName } });
            }

            // Delete all related files for this world
            var deletedFiles = new List<string>();
            var relatedFilePatterns = new[]
            {
                BookmarkController.FileIdentifierLegendsXml,
                BookmarkController.FileIdentifierLegendsPlusXml,
                BookmarkController.FileIdentifierWorldHistoryTxt,
                BookmarkController.FileIdentifierWorldMapBmp,
                BookmarkController.FileIdentifierWorldSitesAndPops
            };

            foreach (var pattern in relatedFilePatterns)
            {
                var relatedFileName = regionId + pattern;
                var relatedFilePath = Path.Combine(_dataDirectory, relatedFileName);
                if (System.IO.File.Exists(relatedFilePath))
                {
                    System.IO.File.Delete(relatedFilePath);
                    deletedFiles.Add(relatedFileName);
                }
            }

            // Delete associated bookmark if it exists
            _bookmarkService.DeleteBookmarkByRegionId(regionId);

            return Ok(new { message = $"World files deleted successfully.", deletedFiles });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting file: {ex.Message}");
        }
    }
}
