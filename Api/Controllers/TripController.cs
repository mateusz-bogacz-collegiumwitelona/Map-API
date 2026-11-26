using Api.Controllers.Base;
using Api.Mappers;
using Api.Request.List;
using Api.Request.Trip;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using Services.Response.Trip;
using Api.Configuration;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/trip")]
    public class TripController : BaseController
    {
        [EndpointSummary("Get trip routes of a user")]
        [EndpointDescription("Returns the routes of all trips of a user, newest first, with stages ordered along the trip. Coordinates are simplified points with latitude and longitude. Without userId it returns the routes of the current user. The optional year matches the calendar year of the trip start date and must be between 1950 and next year. Returns 400 when the year or userId is invalid and 404 when the user does not exist or their profile is private.")]
        [ProducesResponseType(typeof(Result<List<TripRouteResponse>>), StatusCodes.Status200OK)]
        [HttpGet("routes")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> GetTripRoutesAsync(
            [FromQuery] TripFilterRequest request,
            [FromServices] ITripServices trips)
        {
            var result = await trips.GetTripRoutesAsync(
                CurrentUserId,
                request.UserId,
                request.Year);

            return HandleResult(result);
        }

        [EndpointSummary("Get visited countries of a user")]
        [EndpointDescription("Returns the countries visited by a user, ordered by name, with the places visited there and the trips that went through them. Without userId it returns the countries of the current user. The optional year matches the calendar year of the trip start date and must be between 1950 and next year. Returns 400 when the year or userId is invalid and 404 when the user does not exist or their profile is private.")]
        [ProducesResponseType(typeof(Result<List<VisitedCountryResponse>>), StatusCodes.Status200OK)]
        [HttpGet("visited-countries")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> GetVisitedCountriesAsync(
            [FromQuery] TripFilterRequest request,
            [FromServices] ITripServices trips)
        {
            var result = await trips.GetVisitedCountriesAsync(
                CurrentUserId,
                request.UserId,
                request.Year);

            return HandleResult(result);
        }

        [EndpointSummary("Get paginated list of user trips")]
        [EndpointDescription("Returns a paginated, chronologically sorted list of trips for the authenticated user (newest first by default). Each item includes header details, summed distance across all active segments, and distinct transport modes used. Supports optional case-insensitive, accent-insensitive search by title or description, and sorting by startDate, endDate, or title.")]
        [ProducesResponseType(typeof(Result<PagedResult<TripSummaryResponse>>), StatusCodes.Status200OK)]
        [HttpGet]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> GetUserTripsAsync(
            [FromServices] ITripServices trips,
            [FromServices] ListMapper mapper,
            [FromQuery] BasicListRequest request)
        {
            var result = await trips.GetUserTripsAsync(
                mapper.MapBasicList(request),
                CurrentUserId);

            return HandleResult(result);
        }

        [EndpointSummary("Get details of a trip")]
        [EndpointDescription("Returns the complete trip details, including description, dates and ordered segments with endpoint names and coordinates. Only the trip owner can access it. Returns 404 when the trip does not exist or belongs to another user.")]
        [ProducesResponseType(typeof(Result<TripDetailsResponse>), StatusCodes.Status200OK)]
        [HttpGet("details/{tripId:guid}")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> GetTripDetailsAsync(
            [FromRoute] Guid tripId,
            [FromServices] ITripServices trips)
        {
            var result = await trips.GetTripDetailsAsync(CurrentUserId, tripId);
            return HandleResult(result);
        }

        [EndpointSummary("Create a trip")]
        [EndpointDescription("Creates a trip for the authenticated user with its header and ordered route segments. Segment order follows the order in the request. Returns 201 when created and 400 when the date range, route points or segment list is invalid.")]
        [ProducesResponseType(typeof(Result<TripDetailsResponse>), StatusCodes.Status201Created)]
        [HttpPost("create")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> CreateTripAsync(
        [FromBody] UpsertTripRequest request,
        [FromServices] ITripServices trips,
        [FromServices] TripMapper mapper)
        {
            var result = await trips.CreateTripAsync(
                CurrentUserId,
                mapper.MapUpsertTrip(request));

            return HandleResult(result);
        }

        [EndpointSummary("Update a trip")]
        [EndpointDescription("Replaces the header and ordered segments of a trip owned by the authenticated user. The update and visited-country synchronization are performed atomically. Returns 400 when the request is invalid and 404 when the trip does not exist or belongs to another user.")]
        [ProducesResponseType(typeof(Result<TripDetailsResponse>), StatusCodes.Status200OK)]
        [HttpPut("update/{tripId:guid}")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> UpdateTripAsync(
        [FromRoute] Guid tripId,
        [FromBody] UpsertTripRequest request,
        [FromServices] ITripServices trips,
        [FromServices] TripMapper mapper)
        {
            var result = await trips.UpdateTripAsync(
                CurrentUserId,
                tripId,
                mapper.MapUpsertTrip(request));

            return HandleResult(result);
        }

        [EndpointSummary("Delete a trip")]
        [EndpointDescription("Soft-deletes a trip owned by the authenticated user and its active segments. It removes the trip's visited-country associations while preserving countries supported by other trips. Returns 404 when the trip does not exist or belongs to another user.")]
        [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
        [HttpDelete("delete/{tripId:guid}")]
        [Authorize(Policy = AuthorizationConfiguration.UserPolicy)]
        public async Task<IActionResult> DeleteTripAsync(
            [FromRoute] Guid tripId,
            [FromServices] ITripServices trips)
        {
            var result = await trips.DeleteTripAsync(CurrentUserId, tripId);
            return HandleResult(result);
        }
    }
}