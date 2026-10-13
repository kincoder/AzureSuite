namespace Processing.Functions.Tests.TestDoubles
{
    /// <summary>A clock frozen at a fixed instant.</summary>
    public class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
