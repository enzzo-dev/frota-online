using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Terms;

public static class TermErrors
{
    public static readonly Error NotFound =
        new("terms.not_found", "Documento de termos não encontrado.", ErrorType.NotFound);

    public static readonly Error NotActive =
        new("terms.not_active", "Esta versão não está mais ativa. Aceite a versão atual.", ErrorType.Conflict);

    public static readonly Error VersionAlreadyPublished =
        new("terms.version_exists", "Esta versão já foi publicada para este tipo de termo.", ErrorType.Conflict);
}
