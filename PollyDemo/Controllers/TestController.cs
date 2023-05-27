using Microsoft.AspNetCore.Mvc;
using PollyDemo.Services;

namespace PollyDemo.Controllers
{
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        [HttpGet("service1/delay")]
        public async Task GetResultFromService1WithDelayAsync([FromServices] IService1 service, [FromQuery] int delay, CancellationToken cancellationToken = default)
        {
            await service.GetWithDelayAsync(delay, cancellationToken);
        }

        [HttpGet("service1/error")]
        public async Task<IActionResult> GetResultFromService1WithErrorAsync([FromServices] IService1 service, CancellationToken cancellationToken = default)
        {
            var response = await service.GetWithErrorAsync(cancellationToken);
            return Ok(response);
        }

        [HttpGet("service2/delay")]
        public async Task GetResultFromService2WithDelayAsync([FromServices] IService2 service, [FromQuery] int delay, CancellationToken cancellationToken = default)
        {
            await service.GetWithDelayAsync(delay, cancellationToken);
        }

        [HttpGet("service2/error")]
        public async Task<IActionResult> GetResultFromService2WithErrorAsync([FromServices] IService2 service, CancellationToken cancellationToken = default)
        {
            var response = await service.GetWithErrorAsync(cancellationToken);
            return Ok(response);
        }

        [HttpGet("service3/delay")]
        public async Task<IActionResult> GetResultFromService3WithDelayAsync([FromServices] IService2 service, [FromQuery] int delay, CancellationToken cancellationToken = default)
        {
            var response = await service.GetWithDelayAsync(delay, cancellationToken);
            return Ok(response);
        }

        [HttpGet("service3/error")]
        public async Task<IActionResult> GetResultFromService3WithErrorAsync([FromServices] IService3 service, CancellationToken cancellationToken = default)
        {
            var response = await service.GetWithErrorAsync(cancellationToken);
            return Ok(response);
        }
    }
}
