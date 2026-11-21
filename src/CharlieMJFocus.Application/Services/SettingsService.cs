using CharlieMJFocus.Application.Abstractions;
using CharlieMJFocus.Domain.Settings;

namespace CharlieMJFocus.Application.Services;

/// <summary>
/// Holds the current <see cref="AppSettings"/> in memory, persists changes and tells
/// interested parties (timer, theme, tray...) when something changed.
/// </summary>
public sealed class SettingsService(ISettingsStore store)
{
    /// <summary>The current settings. Defaults until <see cref="LoadAsync"/> has run.</summary>
    public AppSettings Current { get; private set; } = new();

    /// <summary>Raised after every successful update, on the calling thread.</summary>
    public event EventHandler<AppSettings>? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Current = (await store.LoadAsync(cancellationToken)).Normalize();
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the current settings, normalises the result,
    /// saves it and raises <see cref="Changed"/>.
    /// </summary>
    public async Task UpdateAsync(
        Func<AppSettings, AppSettings> change,
        CancellationToken cancellationToken = default)
    {
        var updated = change(Current).Normalize();
        await store.SaveAsync(updated, cancellationToken);
        Current = updated;
        Changed?.Invoke(this, updated);
    }
}
