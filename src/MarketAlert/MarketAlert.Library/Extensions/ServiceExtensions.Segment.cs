using MarketAlert.Library.HostedServices;

namespace MarketAlert.Library;

public static partial class ServiceExtensions
{
    private static IServiceCollection AddSegment(
        this IServiceCollection services)
    {
        // --------------------------------------------------
        // Segment precision cache
        // --------------------------------------------------

        services.AddSingleton<
            ISegmentPrecisionCache,
            SegmentPrecisionCache>();

        // --------------------------------------------------
        // Segment repository
        // --------------------------------------------------

        services.AddScoped<
            ISegmentRepository,
            SegmentRepository>();

        // --------------------------------------------------
        // Segment cache warm-up
        // --------------------------------------------------

        services.AddHostedService<
            SegmentPrecisionCacheWarmupService>();

        return services;
    }
}