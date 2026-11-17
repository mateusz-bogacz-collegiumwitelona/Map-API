using Api.Controllers.Base;
using Api.Request.Place;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Place;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/place")]
    [Authorize]
    public class PlaceController : BaseController
    {
        [EndpointSummary("Search places")]
        [EndpointDescription("Searches the available place catalogue by the beginning of a place name. Returns up to 10 matches with country and coordinates. The query must contain between 2 and 100 characters.")]
        [ProducesResponseType(
            typeof(Result<List<PlaceSearchResponse>>),
            StatusCodes.Status200OK)]
        [HttpGet("search")]
        public async Task<IActionResult> SearchPlacesAsync(
            [FromQuery] PlaceSearchRequest request,
            [FromServices] IPlaceServices places)
        {
            var result = await places.SearchPlacesAsync(request.Query ?? string.Empty);
            return HandleResult(result);
        }
    }
}