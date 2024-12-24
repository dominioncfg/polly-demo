using Microsoft.AspNetCore.Mvc;

namespace ExternalApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnotherBackendController : ControllerBase
    {
        private readonly ILogger<AnotherBackendController> _logger;

        public AnotherBackendController(ILogger<AnotherBackendController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("{id:guid}")]
        public Task<UserResponse> GetUser([FromRoute(Name ="id")]Guid id, CancellationToken cancellation)
        {
            var rnd = Random.Shared.NextDouble();

            if (rnd <= 0.6)
                throw new ApplicationException("It just failed");

            return Task.FromResult(new UserResponse(id, $"Perico From External Api- {rnd * 100}"));
        }
    }

    public record UserResponse(Guid Id, string Name);

}
