using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Caesarea.ServiceDefaults;

/// <summary>
/// Adds lightweight DataAnnotations validation to minimal API body endpoints.
/// </summary>
public static class EndpointValidationExtensions
{
    /// <summary>
    /// Validates the first request body argument of type <typeparamref name="TRequest"/> before the route handler runs.
    /// </summary>
    /// <typeparam name="TRequest">The request model type decorated with DataAnnotations.</typeparam>
    /// <param name="builder">The route handler builder to decorate.</param>
    /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for chaining.</returns>
    public static RouteHandlerBuilder ValidateBody<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<DataAnnotationsEndpointFilter<TRequest>>();
    }
}

file sealed class DataAnnotationsEndpointFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        var validationContext = new ValidationContext(request);
        List<ValidationResult> validationResults = [];

        if (Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty).Select(memberName => new
            {
                MemberName = memberName,
                ErrorMessage = result.ErrorMessage ?? "The request body is invalid."
            }))
            .GroupBy(
                item => string.IsNullOrWhiteSpace(item.MemberName)
                    ? string.Empty
                    : JsonNamingPolicy.CamelCase.ConvertName(item.MemberName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        var problem = ProblemDetailsFactory.CreateValidationProblem(errors, context.HttpContext.GetCorrelationId());
        return TypedResults.BadRequest(problem);
    }
}
