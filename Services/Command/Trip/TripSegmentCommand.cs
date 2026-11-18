using Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Command.Trip
{
    public sealed record TripSegmentCommand(
        TripPointCommand Origin,
        TripPointCommand Destination,
        TransportTypeEnum TransportType);
}
