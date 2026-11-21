using System.Windows.Input;

namespace CharlieMJFocus.Presentation.Mvvm;

/// <summary>
/// A command that runs an async delegate. While it is running the command disables itself,
/// which prevents double-clicks from running the same operation twice. Exceptions are never
/// swallowed: they are reported through <see cref="UnhandledException"/> so the app can log them.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute)
        : this(_ => execute())
    {
    }

    public AsyncRelayCommand(Func<object?, Task> execute) => _execute = execute;

    /// <summary>Raised when a command throws. Set once at startup to log the error and inform the user.</summary>
    public static event Action<Exception>? UnhandledException;

    public event EventHandler? CanExecuteChanged;

    public bool IsRunning => _isRunning;

    public bool CanExecute(object? parameter) => !_isRunning;

    // "async void" is required by ICommand.Execute; all exceptions are caught inside.
    public async void Execute(object? parameter)
    {
        if (_isRunning)
        {
            return;
        }

        SetRunning(true);

        try
        {
            await _execute(parameter);
        }
        catch (Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }
        finally
        {
            SetRunning(false);
        }
    }

    private void SetRunning(bool value)
    {
        _isRunning = value;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
