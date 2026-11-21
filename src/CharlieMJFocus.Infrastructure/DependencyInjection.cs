using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Infrastructure.Backup;
using CharlieMJFocus.Infrastructure.Logging;
using CharlieMJFocus.Infrastructure.Persistence;
using CharlieMJFocus.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CharlieMJFocus.Infrastructure;

/// <summary>Registers every infrastructure service with the dependency-injection container.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCharlieMJFocusInfrastructure(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton<SqliteDatabase>();
        services.AddSingleton<DatabaseMigrator>();

        services.AddSingleton<ITodoRepository, SqliteTodoRepository>();
        services.AddSingleton<IFocusSessionRepository, SqliteFocusSessionRepository>();
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<ITimerStateStore, SqliteTimerStateStore>();

        services.AddSingleton<IBackupService, SqliteBackupService>();
        services.AddSingleton<IExportStore, FileExportStore>();
        services.AddSingleton<IAppLogger, FileAppLogger>();

        return services;
    }
}
