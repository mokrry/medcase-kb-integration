using Microsoft.AspNetCore.Mvc;

namespace MedicalFeaturePrototype.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HomeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "MedicalFeaturePrototype.Api",
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
