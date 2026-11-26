using Api.Mappers.Helper;
using Api.Request.Place;
using Riok.Mapperly.Abstractions;
using Services.Command.Place;

namespace Api.Mappers
{
    [Mapper]
    public partial class PlaceMapper : BaseMapper
    {
        [MapProperty(
            nameof(PlaceSearchRequest.Query),
            nameof(PlaceSearchCommand.Query),
            Use = nameof(Trim))]
        public partial PlaceSearchCommand MapSearch(
            PlaceSearchRequest request);
    }
}