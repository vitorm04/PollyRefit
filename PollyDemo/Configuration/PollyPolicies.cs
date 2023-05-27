using Polly;
using Polly.Extensions.Http;
using Polly.Registry;
using PollyDemo.Services;
using Refit;
using Serilog;

namespace PollyDemo.Configuration
{
    public static class PollyPolicies
    {
        public static IServiceCollection AddHttpServices(this IServiceCollection services, IConfiguration configuration)
        {
            //Bind external services
            IDictionary<string, HttpServiceConfiguration> httpServices = configuration.GetSection("ExternalServices")
                .Get<Dictionary<string, HttpServiceConfiguration>>();

            //Create Polly Registry
            var registry = CreatPolicies(httpServices);
            services.AddSingleton<IReadOnlyPolicyRegistry<string>>(registry);

            //Add Refit service + Polly Policies
            services
                .AddHttpService<IService1>("Service1", httpServices["Service1"])
                .AddHttpService<IService2>("Service2", httpServices["Service2"])
                .AddHttpService<IService3>("Service3", httpServices["Service3"]);

            return services;
        }

        private static IServiceCollection AddHttpService<TRefit>(this IServiceCollection services, string serviceName, HttpServiceConfiguration serviceConfiguration) where TRefit : class
        {
            services
                .AddRefitClient<TRefit>()
                .ConfigureHttpClient(httpClient => httpClient.BaseAddress = new Uri(serviceConfiguration.BaseUrl))
                .AddPolicyHandlerFromRegistry(policyKey: serviceName);

            return services;
        }

        public static PolicyRegistry CreatPolicies(IDictionary<string, HttpServiceConfiguration> configurations)
        {
            PolicyRegistry registry = new PolicyRegistry();

            foreach (var serviceConfiguration in configurations)
            {
                var configuration = serviceConfiguration.Value;

                var retryPolicy = Policy
                     .Handle<HttpRequestException>()
                     .OrTransientHttpError()
                     .WaitAndRetryAsync<HttpResponseMessage>(sleepDurations: configuration.GetRetryInterval(), onRetry: (httpContext, retryCount, timespan, context) =>
                     {
                         Log.Warning("Retry policy - Attemp {retryCount}", retryCount);
                     });

                var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
                    timeout: configuration.GeTimeout(),
                    timeoutStrategy: Polly.Timeout.TimeoutStrategy.Optimistic,
                    onTimeoutAsync: (context, timespan, _, _) =>
                    {
                        Log.Warning("Timeout policy after {TimeSpan}", timespan);
                        return Task.CompletedTask;
                    });


                var circuitBreaker = Policy
                      .Handle<HttpRequestException>()
                      .OrTransientHttpError()
                      .AdvancedCircuitBreakerAsync(
                        failureThreshold: configuration.CircuitBreakerFailureThreshold,
                        samplingDuration: configuration.GetCircuitBreakerSamplingDuration(),
                        minimumThroughput: 10,
                        durationOfBreak: configuration.GetCircuitBreakerDuration());


                var defaultHttpMessage = new HttpResponseMessage(statusCode: System.Net.HttpStatusCode.FailedDependency);
                defaultHttpMessage.RequestMessage = new();

                var fallbackPolicy = Policy
                      .Handle<Exception>()
                      .OrTransientHttpError()
                      .FallbackAsync(defaultHttpMessage, onFallbackAsync: (_, _) =>
                      {
                          Log.Warning("Fallback policy");
                          return Task.CompletedTask;
                      });

                var fullPolicy = Policy.WrapAsync(fallbackPolicy, retryPolicy, timeoutPolicy, circuitBreaker);

                registry.Add(serviceConfiguration.Key, fullPolicy);
            }

            return registry;
        }
    }
}
