namespace BoulderTime.Application.Abstractions;

/// <summary>Removes the sign-in behind an account. Implemented against Supabase Auth with the service-role key.</summary>
public interface IAuthAdmin
{
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
