using Api.Controllers.Base;
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

        [EndpointSummary("Get visited countries of a user")]
        [EndpointDescription("Returns the countries visited by a user, ordered by name, with the places visited there and the trips that went through them. Without userId it returns the countries of the current user. The optional year matches the calendar year of the trip start date. Returns 404 when the user does not exist or their profile is private.")]
        [ProducesResponseType(typeof(Result<List<VisitedCountryResponse>>), StatusCodes.Status200OK)]
        [HttpGet("visited-countries")]
        [Authorize]
        public async Task<IActionResult> GetVisitedCountriesAsync(
            [FromQuery] Guid? userId,
            [FromQuery] int? year,
            [FromServices] ITripServices trips)
        {
            var result = await trips.GetVisitedCountriesAsync(CurrentUserId, userId, year);
            return HandleResult(result);
        }
    }
}
