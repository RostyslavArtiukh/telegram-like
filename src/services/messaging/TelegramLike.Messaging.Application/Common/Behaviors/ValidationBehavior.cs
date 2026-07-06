using FluentValidation;
using MediatR;

namespace TelegramLike.Messaging.Application.Common.Behaviors;

/// <summary>
/// Runs all registered FluentValidation validators for a request before the handler
/// executes. Without this behavior the validators (e.g. SendMessageCommandValidator)
/// are never invoked and invalid input reaches the domain layer, where value objects
/// throw raw exceptions. Mirrors the Identity service's pipeline.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
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
