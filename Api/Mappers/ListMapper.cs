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
        [MapProperty(nameof(BasicListRequest.SortBy), nameof(BasicListCommand.SortBy), Use = nameof(ToLowerCase))]
        public partial BasicListCommand MapBasicList(BasicListRequest request);

        [MapProperty(nameof(UserListRequest.SearchTerm), nameof(UserListCommand.SearchTerm), Use = nameof(Trim))]
        [MapProperty(nameof(UserListRequest.SortBy), nameof(UserListCommand.SortBy), Use = nameof(ToLowerCase))]
        public partial UserListCommand MapUserList(UserListRequest request);

        [MapProperty(nameof(ReportListRequest.SearchTerm), nameof(ReportListCommand.SearchTerm), Use = nameof(Trim))]
        [MapProperty(nameof(ReportListRequest.SortBy), nameof(ReportListCommand.SortBy), Use = nameof(ToLowerCase))]
        public partial ReportListCommand ToReportList(ReportListRequest request);

        [MapProperty(nameof(SimpleListRequest.SortBy), nameof(SimpleListCommand.SortBy), Use = nameof(ToLowerCase))]
        public partial SimpleListCommand ToSimpleList(SimpleListRequest request);
    }
}
