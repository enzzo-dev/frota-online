using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Terms;

public static class TermErrors
{
    public static readonly Error NotFound =
        new("terms.not_found", "Term document not found.", ErrorType.NotFound);

    public static readonly Error NotActive =
        new("terms.not_active", "This version is no longer active. Accept the current version instead.", ErrorType.Conflict);

    public static readonly Error VersionAlreadyPublished =
        new("terms.version_exists", "This version was already published for this term type.", ErrorType.Conflict);
}
