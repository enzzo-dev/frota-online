using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace FrotaOnline.Api.Extensions;

/// <summary>
/// ASP.NET writes its own binding errors (malformed JSON, wrong type, missing body) in English. The app is pt-BR, so the
/// texts the framework produces are replaced here; the application's own messages are already written in Portuguese.
/// </summary>
internal static class PortugueseModelBindingMessages
{
    public static void Apply(DefaultModelBindingMessageProvider provider)
    {
        provider.SetValueMustNotBeNullAccessor(_ => "O campo é obrigatório.");
        provider.SetMissingBindRequiredValueAccessor(name => $"O campo {name} é obrigatório.");
        provider.SetMissingKeyOrValueAccessor(() => "É necessário informar um valor.");
        provider.SetMissingRequestBodyRequiredValueAccessor(() => "O corpo da requisição é obrigatório.");
        provider.SetAttemptedValueIsInvalidAccessor((value, name) => $"O valor '{value}' não é válido para {name}.");
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"O valor '{value}' não é válido.");
        provider.SetUnknownValueIsInvalidAccessor(name => $"O valor informado não é válido para {name}.");
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => "O valor informado não é válido.");
        provider.SetValueIsInvalidAccessor(value => $"O valor '{value}' não é válido.");
        provider.SetValueMustBeANumberAccessor(name => $"O campo {name} deve ser um número.");
        provider.SetNonPropertyValueMustBeANumberAccessor(() => "O campo deve ser um número.");
    }

    /// <summary>Translates the default titles the framework puts on its problem responses.</summary>
    public static void TranslateProblem(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Title = problem.Title switch
        {
            "One or more validation errors occurred." => "Um ou mais erros de validação ocorreram.",
            "An error occurred while processing your request." => "Ocorreu um erro ao processar a requisição.",
            _ => problem.Title
        };
    }
}
