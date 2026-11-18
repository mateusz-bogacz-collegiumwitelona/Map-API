using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Command.Trip
{
    public sealed record TripPointCommand(
        string Name,
        double Latitude,
        double Longitude);
}
