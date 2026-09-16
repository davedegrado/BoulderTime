using BoulderTime.Application.Abstractions;

namespace BoulderTime.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
