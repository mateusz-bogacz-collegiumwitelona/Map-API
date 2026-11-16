using Api.Mappers.Helper;
using Api.Request.Ban;
using Riok.Mapperly.Abstractions;
using Services.Command.Ban;

namespace Api.Mappers
{
    [Mapper]
    public partial class BanMapper : BaseMapper
    {
        [MapProperty(nameof(ReportUserRequest.Reason), nameof(ReportUserCommand.Reason), Use = nameof(Trim))]
        public partial ReportUserCommand ToReport(ReportUserRequest request);
    }
}
