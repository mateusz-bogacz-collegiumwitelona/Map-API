using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Trip;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/trip")]
    public class TripController : BaseController
    {
        [EndpointSummary("Get trip routes of a user")]
        [EndpointDescription("Returns the routes of all trips of a user, newest first, with stages ordered along the trip. Coordinates are simplified points with latitude and longitude. Without userId it returns the routes of the current user. The optional year matches the calendar year of the trip start date. Returns 404 when the user does not exist or their profile is private.")]
        [ProducesResponseType(typeof(Result<List<TripRouteResponse>>), StatusCodes.Status200OK)]
        [HttpGet("routes")]
        [Authorize]
        public async Task<IActionResult> GetTripRoutesAsync(
            [FromQuery] Guid? userId,
            [FromQuery] int? year,
            [FromServices] ITripServices trips)
        {
            var result = await trips.GetTripRoutesAsync(CurrentUserId, userId, year);
            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of user trips")]
        [EndpointDescription("Returns a paginated, chronologically sorted list of trips for the authenticated user (newest first by default). Each item includes header details, summed distance across all active segments, and distinct transport modes used. Supports optional case-insensitive, accent-insensitive search by title or description, and sorting by startDate, endDate, or title.")]
        [ProducesResponseType(typeof(Result<PagedResult<TripSummaryResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize]

        public async Task<IActionResult> GetUserTripsAsync(
            [FromServices] ITripServices trips,
            [FromServices] ListMapper mapper,
            [FromQuery] BasicListRequest request
            )
        {
            var result = await trips.GetUserTripsAsync(mapper.MapBasicList(request), CurrentUserId);
            return HandleResult(result);
        }
    }
}
