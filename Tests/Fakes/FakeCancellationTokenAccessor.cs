using Services.Accessors.Interfaces;

namespace Tests.Fakes
{
    internal class FakeCancellationTokenAccessor : ICancellationTokenAccessor
    {
        public CancellationToken Token => CancellationToken.None;
    }
}
