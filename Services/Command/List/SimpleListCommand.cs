using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Command.List
{
    public record SimpleListCommand
    {
        public int? PageNumber { get; init; }
        public int? PageSize { get; init; }
        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;
    }
}
