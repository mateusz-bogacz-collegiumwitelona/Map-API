using Api.Request.List;
using Riok.Mapperly.Abstractions;
using Services.Command.List;

namespace Api.Mappers
{
    [Mapper]
    public partial class ListMapper
    {
        public partial BasicListCommand MapBasicList(BasicListRequest request);
    }
}
