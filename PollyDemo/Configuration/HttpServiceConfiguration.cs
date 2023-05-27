namespace PollyDemo.Configuration
{
    public record HttpServiceConfiguration
    {
        public string BaseUrl { get; set; }
        public short TimeoutInMilliseconds { get; init; }
        public short[] RetryInterval { get; init; }
        public float CircuitBreakerFailureThreshold { get; init; }
        public short CircuitBreakerDurationInSeconds { get; init; }
        public short CircuitBreakerSamplingDurationInMilliseconds { get; init; }
        public short CircuitBreakerDurationInMilliseconds { get; init; }
        

        public IEnumerable<TimeSpan> GetRetryInterval() => RetryInterval.Select(interval => TimeSpan.FromMilliseconds(interval));
        public TimeSpan GeTimeout() => TimeSpan.FromMilliseconds(TimeoutInMilliseconds);
        public TimeSpan GetCircuitBreakerDuration() => TimeSpan.FromMilliseconds(CircuitBreakerDurationInMilliseconds);
        public TimeSpan GetCircuitBreakerSamplingDuration() => TimeSpan.FromMilliseconds(CircuitBreakerSamplingDurationInMilliseconds);
    }
}