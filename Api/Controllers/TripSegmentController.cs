using Api.Configuration;
using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.Trip;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/trip/segments")]
    public class TripSegmentController : BaseController
    {
        [EndpointSummary("Save the route of a trip segment")]
        [EndpointDescription("Stores the route calculated on the device for one stage of the current user's trip, together with its distance in kilometers, so trip queries return it directly instead of routing again. Coordinates are points with latitude and longitude ordered from the origin to the destination of the segment, between 2 and 10000 points. The route must start within 10 km of the segment origin and end within 10 km of its destination. The distance must be between 0.01 and 40075 km and cannot be shorter than the length of the route line. Returns 400 when the route or distance is invalid and 404 when the segment does not exist or belongs to another user. Available only for accounts with the User role.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpPut("{segmentId:guid}/route")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> UpdateRouteAsync(
            [FromRoute] Guid segmentId,
            [FromServices] ITripSegmentServices segments,
            [FromServices] TripMapper mapper,
            [FromBody] UpdateSegmentRouteRequest request)
        {
            var result = await segments.UpdateRouteAsync(CurrentUserId, segmentId, mapper.MapUpdateSegmentRoute(request));
            return HandleResult(result);
        }
    }
}
