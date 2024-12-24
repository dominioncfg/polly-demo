using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using System.Text.Json;
public interface IUserResilientTestService
{
    public Task<UserResponse> GetUserWithFaultTolerance(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserWithFallback(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserWithTimeout(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserIoCPipeline(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserWithCircuitBreaker(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserWithFaultToleranceWithFallback(string userId, CancellationToken cancellationToken);

    public Task<UserResponse> GetUserWithHttpDefaultResilience(string userId, CancellationToken cancellationToken);
}

public record UserResponse(Guid Id, string Name);

public class UserResilientTestService : IUserResilientTestService
{
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly ILogger<UserResilientTestService> _logger;
    private readonly HttpClient _httpClient;

    public UserResilientTestService(ResiliencePipelineProvider<string> resiliencePipeline, HttpClient httpClient, ILogger<UserResilientTestService> logger)
    {
        _resiliencePipelineProvider = resiliencePipeline;
        _httpClient = httpClient;
        _logger = logger;        
    }

    public async Task<UserResponse> GetUserWithFaultTolerance(string userId, CancellationToken cancellationToken)
    {
       var pipeline = new ResiliencePipelineBuilder<UserResponse>()
            .AddRetry(new Polly.Retry.RetryStrategyOptions<UserResponse>()
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder<UserResponse>()
                .Handle<ApplicationException>(),
                OnRetry = retryArgs =>
                {
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        var user = await pipeline.ExecuteAsync(async token => await GetTheUser(0.7, token),cancellationToken);

        return user;
    }

    public async Task<UserResponse> GetUserWithFallback(string userId, CancellationToken cancellationToken)
    {
        var pipeline = new ResiliencePipelineBuilder<UserResponse>()
             .AddFallback(new Polly.Fallback.FallbackStrategyOptions<UserResponse>()
             {
                 FallbackAction = _ => Outcome.FromResultAsValueTask(new UserResponse(Guid.Empty, "DefaultPolicyResponse"))
             })
             .Build();

        var user = await pipeline.ExecuteAsync(async token => await GetTheUser(0.7, token), cancellationToken);

        return user;
    }

    public async Task<UserResponse> GetUserIoCPipeline(string userId, CancellationToken cancellationToken)
    {
        var pipeline = _resiliencePipelineProvider.GetPipeline<UserResponse>("ioc-registered-pipeline");

        var user = await pipeline.ExecuteAsync(async token => await GetTheUser(0.7, token), cancellationToken);

        return user;
    }

    public async Task<UserResponse> GetUserWithTimeout(string userId, CancellationToken cancellationToken)
    {
        var pipeline = new ResiliencePipelineBuilder<UserResponse>()
             .AddTimeout(TimeSpan.FromSeconds(1))
             .Build();

        try
        {
            //callback needs to use the cancellation token otherwise is not going to work
            var user = await pipeline.ExecuteAsync(async token => await GetTheUserWithDelay(0.5, token), cancellationToken);

            return user;
        }
        catch(TimeoutRejectedException ex)
        {
            _logger.LogError(ex,"Operation timed out please try again.");
            throw;
        }
        
    }

    public async Task<UserResponse> GetUserWithCircuitBreaker(string userId, CancellationToken cancellationToken)
    {
        var pipeline = new ResiliencePipelineBuilder<UserResponse>()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<UserResponse>
        {
            FailureRatio = 0.3,
            SamplingDuration = TimeSpan.FromSeconds(1),
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<UserResponse>().Handle<ApplicationException>()
        })
        .Build();

        UserResponse user = null;
        int failedAttemps = 0;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                user = await pipeline.ExecuteAsync(async token => await GetTheUser(0.2, token), cancellationToken);
            }
            catch (ApplicationException)
            {
                _logger.LogWarning("Operation failed please try again.");
                failedAttemps++;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError(ex,"Operation failed too many times please try again later.");
                throw;
            }
        }



        return new UserResponse(user!.Id,$"{user.Name}- Failed {failedAttemps} times");
    }

    public async Task<UserResponse> GetUserWithFaultToleranceWithFallback(string userId, CancellationToken cancellationToken)
    {
        var pipeline = new ResiliencePipelineBuilder<UserResponse>()
              //Outer Most
              .AddFallback(new Polly.Fallback.FallbackStrategyOptions<UserResponse>()
              {
                  FallbackAction = _ => Outcome.FromResultAsValueTask(new UserResponse(Guid.Empty, "Fallback after all retries"))
              })
              //Inner Most
             .AddRetry(new Polly.Retry.RetryStrategyOptions<UserResponse>()
             {
                 MaxRetryAttempts = 2,
                 BackoffType = DelayBackoffType.Constant,
                 Delay = TimeSpan.Zero,
                 ShouldHandle = new PredicateBuilder<UserResponse>()
                 .Handle<ApplicationException>(),
                 OnRetry = retryArgs =>
                 {
                     return ValueTask.CompletedTask;
                 }
             })
             .Build();

        var user = await pipeline.ExecuteAsync(async token => await GetTheUser(0.999, token), cancellationToken);

        return user;
    }

    public async Task<UserResponse> GetUserWithHttpDefaultResilience(string userId, CancellationToken cancellationToken)
    {
        var endpointUrl = $"anotherbackend/{userId}";

        var response =  await _httpClient.GetAsync(endpointUrl, cancellationToken);

        response.EnsureSuccessStatusCode();

        var userResponse = await response.Content.ReadAsStringAsync();

        var user = JsonSerializer.Deserialize<UserResponse>(userResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return user!;
    }

    
    private static async Task<UserResponse> GetTheUserWithDelay(double probabilityOfFailure, CancellationToken cancellationToken)
    {
        var rnd = Random.Shared.NextDouble();

        if (rnd <= probabilityOfFailure)
            await Task.Delay(3000, cancellationToken);

        return new UserResponse(Guid.NewGuid(), $"Perico - {rnd * 100}");
    }

    private static Task<UserResponse> GetTheUser(double probabilityOfFailure, CancellationToken cancellationToken)
    {
        var rnd = Random.Shared.NextDouble();
        
        if (rnd <= probabilityOfFailure)
            throw new ApplicationException("It just failed"); 

        return Task.FromResult(new UserResponse(Guid.NewGuid(), $"Perico - {rnd * 100}"));
    }
}


