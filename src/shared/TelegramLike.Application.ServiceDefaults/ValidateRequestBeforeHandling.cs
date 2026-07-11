using FluentValidation;
using MediatR;

namespace TelegramLike.Application.ServiceDefaults;

/// <summary>
/// Runs all registered FluentValidation validators for a request before the
/// handler executes. Without this step the validators are never invoked and
/// invalid input reaches the domain layer, where value objects throw raw
/// exceptions instead of a proper validation error.
/// </summary>
public sealed class ValidateRequestBeforeHandling<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
