using Domain.Common;
using Services.Response.Place;

namespace Services.Interfaces
{
    public interface IPlaceServices
    {
        Task<Result<List<PlaceSearchResponse>>> SearchPlacesAsync(string query);
    }
}