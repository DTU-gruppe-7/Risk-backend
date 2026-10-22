namespace Risk.Application.Interfaces;

/// <summary>The authenticated caller. Implemented by the API from the JWT claims.</summary>
public interface ICurrentUser
{
    /// <summary>The domain user id (the JWT "sub" claim).</summary>
    Guid UserId { get; }
}
