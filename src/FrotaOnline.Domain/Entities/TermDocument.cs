using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class TermDocument : BaseEntity
{
    private TermDocument()
    {
    }

    public TermType Type { get; private set; }

    public string Version { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string ContentUrl { get; private set; } = string.Empty;

    public DateTime PublishedAt { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static TermDocument Create(TermType type, string version, string title, string contentUrl, DateTime publishedAt) => new()
    {
        Type = type,
        Version = Guard.NotNullOrWhiteSpace(version, nameof(version)),
        Title = Guard.NotNullOrWhiteSpace(title, nameof(title)),
        ContentUrl = Guard.NotNullOrWhiteSpace(contentUrl, nameof(contentUrl)),
        PublishedAt = publishedAt
    };

    public void Deactivate() => IsActive = false;
}
