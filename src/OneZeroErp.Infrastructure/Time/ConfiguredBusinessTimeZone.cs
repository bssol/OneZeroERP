using Microsoft.Extensions.Configuration;
using OneZeroErp.Application.Time;

namespace OneZeroErp.Infrastructure.Time;

public static class ConfiguredBusinessTimeZone
{
    public static TimeZoneInfo Resolve(IConfiguration configuration) =>
        BusinessTimeZone.Resolve(configuration["Company:TimeZoneId"] ?? configuration["Accounting:TimeZoneId"]);
}