using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Terms.Dtos;

public sealed record TermResponse(Guid Id, TermType Type, string Version, string Title, string ContentUrl, DateTime PublishedAt);

public sealed record AcceptTermRequest(Guid TermDocumentId);

public sealed record ConsentResponse(Guid TermDocumentId, TermType Type, string Version, DateTime AcceptedAt);

public sealed record PublishTermRequest(TermType Type, string Version, string Title, string ContentUrl);
