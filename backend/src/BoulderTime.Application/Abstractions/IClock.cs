namespace BoulderTime.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
