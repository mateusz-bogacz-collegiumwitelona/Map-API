using Domain.Common;
using Services.Command.Place;
using Services.Response.Place;

namespace Services.Interfaces
{
    public interface IPlaceServices
    {
        Task<Result<List<PlaceSearchResponse>>> SearchPlacesAsync(PlaceSearchCommand command);
    }
}