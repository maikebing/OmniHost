namespace OmniHost;

/// <summary>
/// Represents a strategy for how a native host window presents and manages its frame.
/// </summary>
public interface IWindowFrameStrategy
{
    /// <summary>
    /// Gets a stable identifier for this frame strategy.
    /// </summary>
    string StrategyId { get; }
}
