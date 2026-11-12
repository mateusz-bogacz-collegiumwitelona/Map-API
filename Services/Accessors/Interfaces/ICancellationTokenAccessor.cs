namespace Services.Accessors.Interfaces
{
    public interface ICancellationTokenAccessor
    {
        CancellationToken Token { get; }
        CancellationToken ResolveCancellationToken(CancellationToken cancellationToken);
    }
}
