using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Admin;

public static class ReviewErrors
{
    public static readonly Error DriverNotFound =
        new("admin.driver_not_found", "Motorista não encontrado.", ErrorType.NotFound);

    public static readonly Error FileNotFound =
        new("admin.file_not_found", "Cadastro do motorista não encontrado.", ErrorType.NotFound);

    public static readonly Error NotPendingReview =
        new("admin.not_pending_review", "Só motoristas em análise podem ser aprovados ou reprovados.", ErrorType.Conflict);

    public static readonly Error IncompleteFile =
        new("admin.incomplete_file", "O motorista não tem veículo ativo ou documento. Reprove o cadastro para que ele possa corrigir.", ErrorType.Conflict);
}
