# .NET - Polly Custom Policies by Service

Esse projeto tem como objetivo, desmostrar uma implementação da biblioteca Polly com regras de timeout/retry/circuitBreaker personalizadas por serviço. 

# Instalação

Para iniciar, vamos instalar os seguintes pacotes para contemplar a solução:


    Polly
    Polly.Extensions.Http
    Refit
    Refit.HttpClientFactory
    Serilog
    Serilog.AspNetCore
    Serilog.Sinks.Console

# Criação das entradas no AppSettings

Vamos utilizar o arquivo de AppSettings.json para armazenar a configuração desejada para cada serviço externo http, dessa forma, conseguimos ter regras diferentes para cada um.

    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      },
      "AllowedHosts": "*",
      "ExternalServices": {
        "Service1": {
          "BaseUrl": "https://httpstat.us/",
          "TimeoutInMilliseconds": 5000,
          "RetryInterval": [
            100,
            500,
            1000,
            2000
          ],
          "CircuitBreakerFailureThreshold": 0.5,
          "CircuitBreakerDurationInMilliseconds": 6000,
          "CircuitBreakerSamplingDurationInMilliseconds": 3000
        },
        "Service2": {
          "BaseUrl": "https://httpstat.us/",
          "TimeoutInMilliseconds": 3000,
          "RetryInterval": [
            100,
            500
          ],
          "CircuitBreakerFailureThreshold": 0.2,
          "CircuitBreakerDurationInMilliseconds": 5000,
          "CircuitBreakerSamplingDurationInMilliseconds": 2500
        },
        "Service3": {
          "BaseUrl": "https://httpstat.us/",
          "TimeoutInMilliseconds": 10000,
          "RetryInterval": [
            100,
            500,
            600,
            900
          ],
          "CircuitBreakerFailureThreshold": 0.8,
          "CircuitBreakerDurationInMilliseconds": 5000,
          "CircuitBreakerSamplingDurationInMilliseconds": 2000
        }
      }
    }


## Criação de um arquivo para bind 

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

## Criação da estrutura de inject das policies

Essa é a estrutura responsável por criar as políticas de acordo com as configurações informadas no AppSettings.json, criando um bind entre o HttpClient e a cadeia de políticas registradas no serviço global.

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


## Utilização

Pronto, agora é só importar o serviço e ele já terá todas as regras do Polly aplicadas. 

### Serviço

    using Microsoft.AspNetCore.Mvc;
    using Refit;
    
    namespace PollyDemo.Services
    {
        public interface IService1
        {
            [Get("/200?sleep={delay}")]
            public Task<IApiResponse> GetWithDelayAsync(int delay, CancellationToken cancellationToken);
    
            [Get("/500")]
            public Task<IApiResponse> GetWithErrorAsync(CancellationToken cancellationToken);
        }
    }



### Controller

    namespace PollyDemo.Controllers
    {
        [Route("api/test")]
        public class TestController : ControllerBase
        {
            [HttpGet]
            public async Task<IActionResult> GetResultFromService1([FromServices] IService1 service, CancellationToken cancellationToken = default)
            {
                var response = await service.GetInformation(cancellationToken);
                return Ok(response);
            }
        }
    }
    

### Timeout
![image](https://github.com/vitorm04/PollyRefit/assets/24477296/ac5e1ad3-f59b-4a85-a039-00fe07d77dc4)


### Retry
![image](https://github.com/vitorm04/PollyRefit/assets/24477296/6da246d6-793d-4962-85d4-efa2983004d5)

    
    
