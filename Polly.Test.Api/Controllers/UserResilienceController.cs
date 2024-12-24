using Microsoft.AspNetCore.Mvc;

namespace Polly.Test.Api.Controllers
{
    /*
     Resilience strategies are essential components of Polly, designed to execute user-defined callbacks while adding an extra layer of resilience. These strategies can't be executed directly; they must be run through a resilience pipeline. Polly provides an API to construct resilience pipelines by incorporating one or more resilience strategies through the pipeline builders.

Polly categorizes resilience strategies into two main groups:

    Reactive: These strategies handle specific exceptions that are thrown, or results that are returned, by the callbacks executed through the strategy.
    Proactive: Unlike reactive strategies, proactive strategies do not focus on handling errors by the callbacks might throw or return. They can make proactive decisions to cancel or reject the execution of callbacks (e.g., using a rate limiter or a timeout resilience strategy).
 
    Reactive are: Retry, Circuit Breaker, Fallback, Hedging
    Proactive are: Timeout, Rate Limiter.
     */

    [ApiController]
    [Route("[controller]")]
    public class UserResilienceController : ControllerBase
    {
        private readonly ILogger<UserResilienceController> _logger;
        private readonly IUserResilientTestService _service;

        public UserResilienceController(ILogger<UserResilienceController> logger, IUserResilientTestService service)
        {
            _logger = logger;
            _service = service;
        }

        [HttpGet]
        [Route("retrys")]
        public Task<UserResponse> GetFaultTolerance(CancellationToken cancellationToken)
        {
            return _service.GetUserWithFaultTolerance(Guid.NewGuid().ToString(), cancellationToken);
        }


        [HttpGet]
        [Route("fallback")]
        public Task<UserResponse> GetFallBack(CancellationToken cancellationToken)
        {
            return _service.GetUserWithFallback(Guid.NewGuid().ToString(), cancellationToken);
        }

        [HttpGet]
        [Route("ioc-registered-pipeline")]
        public Task<UserResponse> GetIoCRegisteredPipeline(CancellationToken cancellationToken)
        {
            return _service.GetUserIoCPipeline(Guid.NewGuid().ToString(), cancellationToken);
        }

        [HttpGet]
        [Route("timeout")]
        public Task<UserResponse> GetTimeout(CancellationToken cancellationToken)
        {
            return _service.GetUserWithTimeout(Guid.NewGuid().ToString(), cancellationToken);
        }

        [HttpGet]
        [Route("circuit-breaker")]
        public Task<UserResponse> GetCircuitBreaker(CancellationToken cancellationToken)
        {
            return _service.GetUserWithCircuitBreaker(Guid.NewGuid().ToString(), cancellationToken);
        }

        [HttpGet]
        [Route("combined-retry-fallback")]
        public Task<UserResponse> GetRetyWithFallback(CancellationToken cancellationToken)
        {
            return _service.GetUserWithFaultToleranceWithFallback(Guid.NewGuid().ToString(), cancellationToken);
        }

        [HttpGet]
        [Route("external-api-default-http-resilience")]
        public Task<UserResponse> GetRetyWithDefaultHttpResilience(CancellationToken cancellationToken)
        {
            return _service.GetUserWithHttpDefaultResilience(Guid.NewGuid().ToString(), cancellationToken);
        }
    }
}
