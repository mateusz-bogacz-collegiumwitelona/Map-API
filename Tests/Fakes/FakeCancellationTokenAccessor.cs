using Services.Accessors.Interfaces;

namespace Tests.Fakes
{
    internal class FakeCancellationTokenAccessor : ICancellationTokenAccessor
    {
        public CancellationToken Token { get; set; } = CancellationToken.None;

        public CancellationToken ResolveCancellationToken(CancellationToken cancellationToken)
            => cancellationToken.CanBeCanceled
                ? cancellationToken
                : Token;
    }
}
