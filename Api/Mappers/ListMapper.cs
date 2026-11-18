using Api.Mappers.Helper;
using Api.Request.List;
using Riok.Mapperly.Abstractions;
using Services.Command.List;

namespace Api.Mappers
{
    [Mapper]
    public partial class ListMapper : BaseMapper
    {
        [MapProperty(nameof(BasicListRequest.SearchTerm), nameof(BasicListCommand.SearchTerm), Use = nameof(Trim))]
        public partial BasicListCommand MapBasicList(BasicListRequest request);

        [MapProperty(nameof(UserListRequest.SearchTerm), nameof(UserListCommand.SearchTerm), Use = nameof(Trim))]
        public partial UserListCommand MapUserList(UserListRequest request);
    }
}
