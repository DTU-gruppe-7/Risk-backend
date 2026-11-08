using Microsoft.AspNetCore.SignalR;
using Risk.Application.Exceptions;
using Risk.Domain.Exceptions;

namespace Risk.Api.Realtime;

/// <summary>
/// The hub counterpart to <see cref="ErrorHandling.ProblemDetailsExceptionHandler"/>. SignalR hides the
/// message of every exception except <see cref="HubException"/>, so known domain and application
/// errors are turned into one and the client sees why a call was refused. Unknown errors stay hidden.
/// </summary>
public sealed class RealtimeExceptionFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception exception) when (exception is
            ValidationFailedException or DomainValidationException or BusinessRuleException or
            NotFoundException or ForbiddenException)
        {
            throw new HubException(exception.Message);
        }
    }
}
