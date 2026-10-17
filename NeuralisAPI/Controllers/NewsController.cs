using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Admin;
using NeuralisAPI.Contracts.Common;
using NeuralisAPI.Data;
using NeuralisAPI.Models;

namespace NeuralisAPI.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly AppDbContext _db;

    public NewsController(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // PUBLIC — list published articles
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category = null)
    {
        var query = _db.News
            .AsNoTracking()
            .Where(n => n.Status == (int)NewsStatus.Published);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(n => n.Category.ToLower() == cat);
        }

        var news = await query
            .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
            .Select(n => Map(n))
            .ToListAsync();

        return Ok(news);
    }

    // =========================================================
    // PUBLIC — single (published only)
    // =========================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null || news.Status != (int)NewsStatus.Published)
            return NotFound();

        return Ok(Map(news));
    }

    // =========================================================
    // PUBLIC — increment view counter (idempotent best-effort)
    // =========================================================
    [HttpPost("{id}/view")]
    public async Task<IActionResult> RegisterView(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null || news.Status != (int)NewsStatus.Published)
            return NotFound();

        news.Views++;
        await _db.SaveChangesAsync();
        return Ok(new { news.Views });
    }

    // =========================================================
    // ADMIN — list all articles (drafts + published + archived)
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> GetAllForAdmin(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null)
    {
        var query = _db.News.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToLowerInvariant();
            int? code = s switch
            {
                "draft" => (int)NewsStatus.Draft,
                "published" => (int)NewsStatus.Published,
                "archived" => (int)NewsStatus.Archived,
                _ => null
            };
            if (code.HasValue)
                query = query.Where(n => n.Status == code.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(n => n.Category.ToLower() == cat);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(n =>
                n.Title.ToLower().Contains(q) ||
                n.Subtitle.ToLower().Contains(q) ||
                n.Summary.ToLower().Contains(q) ||
                n.Tags.ToLower().Contains(q));
        }

        var news = await query
            .OrderByDescending(n => n.UpdatedAt ?? n.PublishedAt ?? n.CreatedAt)
            .Select(n => Map(n))
            .ToListAsync();

        return Ok(news);
    }

    // =========================================================
    // ADMIN — single (any status)
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/{id}")]
    public async Task<IActionResult> GetByIdForAdmin(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();
        return Ok(Map(news));
    }

    // =========================================================
    // ADMIN — create
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(NewsRequest request)
    {
        var status = ResolveStatus(request);

        var news = new News
        {
            Title = request.Title,
            Subtitle = request.Subtitle ?? "",
            Summary = request.Summary ?? "",
            Content = request.Content,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim().ToLowerInvariant(),
            Tags = request.Tags ?? "",
            CoverImageUrl = request.CoverImageUrl ?? "",
            Status = (int)status,
            Priority = request.Priority,
            IsFeatured = request.IsFeatured,
            IsPublished = status == NewsStatus.Published,
            AuthorName = ResolveAuthorName(),
            AuthorId = ResolveAuthorId(),
            CreatedAt = DateTime.UtcNow,
            PublishedAt = status == NewsStatus.Published
                ? (request.PublishedAt ?? DateTime.UtcNow)
                : null,
            UpdatedAt = null,
            Views = 0,
        };

        _db.News.Add(news);
        await _db.SaveChangesAsync();

        return Ok(Map(news));
    }

    // =========================================================
    // ADMIN — update
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, NewsRequest request)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();

        var status = ResolveStatus(request);
        var wasPublished = news.Status == (int)NewsStatus.Published;

        news.Title = request.Title;
        news.Subtitle = request.Subtitle ?? "";
        news.Summary = request.Summary ?? "";
        news.Content = request.Content;
        news.Category = string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim().ToLowerInvariant();
        news.Tags = request.Tags ?? "";
        news.CoverImageUrl = request.CoverImageUrl ?? "";
        news.Status = (int)status;
        news.Priority = request.Priority;
        news.IsFeatured = request.IsFeatured;
        news.IsPublished = status == NewsStatus.Published;
        news.UpdatedAt = DateTime.UtcNow;

        if (status == NewsStatus.Published && !wasPublished)
            news.PublishedAt = request.PublishedAt ?? DateTime.UtcNow;
        else if (status == NewsStatus.Published && request.PublishedAt.HasValue)
            news.PublishedAt = request.PublishedAt;

        await _db.SaveChangesAsync();
        return Ok(Map(news));
    }

    // =========================================================
    // ADMIN — state transitions
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();

        news.Status = (int)NewsStatus.Published;
        news.IsPublished = true;
        news.PublishedAt ??= DateTime.UtcNow;
        news.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Map(news));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();

        news.Status = (int)NewsStatus.Draft;
        news.IsPublished = false;
        news.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Map(news));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();

        news.Status = (int)NewsStatus.Archived;
        news.IsPublished = false;
        news.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Map(news));
    }

    // =========================================================
    // ADMIN — delete
    // =========================================================
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var news = await _db.News.FindAsync(id);
        if (news == null)
            return NotFound();

        _db.News.Remove(news);
        await _db.SaveChangesAsync();
        return Ok(new { message = "News deleted" });
    }

    // =========================================================
    // Helpers
    // =========================================================
    private static NewsStatus ResolveStatus(NewsRequest request)
    {
        if (request.Status.HasValue)
        {
            return (NewsStatus)request.Status.Value switch
            {
                NewsStatus.Draft => NewsStatus.Draft,
                NewsStatus.Published => NewsStatus.Published,
                NewsStatus.Archived => NewsStatus.Archived,
                _ => request.IsPublished ? NewsStatus.Published : NewsStatus.Draft
            };
        }
        return request.IsPublished ? NewsStatus.Published : NewsStatus.Draft;
    }

    private string ResolveAuthorName()
    {
        var name = User?.FindFirst("fullName")?.Value
                   ?? User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                   ?? User?.FindFirst("name")?.Value
                   ?? User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                   ?? User?.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(name) ? "Neuralis Admin" : name!;
    }

    private Guid? ResolveAuthorId()
    {
        var id = User?.FindFirst("id")?.Value
                 ?? User?.FindFirst("userId")?.Value
                 ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var g) ? g : null;
    }

    private static NewsResponse Map(News n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Subtitle = n.Subtitle ?? "",
        Summary = n.Summary ?? "",
        Content = n.Content,
        Category = string.IsNullOrWhiteSpace(n.Category) ? "general" : n.Category,
        Tags = n.Tags ?? "",
        CoverImageUrl = n.CoverImageUrl ?? "",
        AuthorName = string.IsNullOrWhiteSpace(n.AuthorName) ? "Neuralis Admin" : n.AuthorName,
        AuthorId = n.AuthorId,
        Status = n.Status,
        Priority = n.Priority,
        IsFeatured = n.IsFeatured,
        IsPublished = n.Status == (int)NewsStatus.Published || n.IsPublished,
        CreatedAt = n.CreatedAt,
        PublishedAt = n.PublishedAt,
        UpdatedAt = n.UpdatedAt,
        Views = n.Views,
    };
}
