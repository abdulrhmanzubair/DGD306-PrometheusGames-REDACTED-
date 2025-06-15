/// <summary>
/// Defines different optimization levels for enemy AI performance
/// Used to control how much processing power each enemy uses based on distance/visibility
/// </summary>
public enum OptimizationLevel
{
    Full,       // Full AI, all updates, all features enabled
    Reduced,    // Reduced update frequency, some features disabled
    Minimal,    // Very basic behavior only, most features disabled
    Disabled    // Completely disabled, object deactivated
}