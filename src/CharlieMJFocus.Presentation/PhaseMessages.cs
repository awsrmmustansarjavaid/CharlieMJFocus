using CharlieMJFocus.Domain.Enums;
using CharlieMJFocus.Domain.Settings;
using CharlieMJFocus.Domain.Timer;

namespace CharlieMJFocus.Presentation;

/// <summary>Wording for the notification shown when a timer phase ends.</summary>
public static class PhaseMessages
{
    /// <summary>
    /// Returns the notification text, or null when the event is not worth interrupting the user for
    /// (abandoned or skipped sessions, stopwatch runs).
    /// </summary>
    /// <param name="result">The phase that just ended.</param>
    /// <param name="nextPhase">The phase the engine moved on to.</param>
    public static (string Title, string Message)? Describe(
        PhaseResult result, TimerPhase nextPhase, TimerSettings settings)
    {
        if (result.Mode != TimerMode.Pomodoro || result.Outcome != SessionOutcome.Completed)
        {
            return null;
        }

        if (result.Phase == TimerPhase.Focus)
        {
            return nextPhase == TimerPhase.LongBreak
                ? ("Focus session complete", $"Great work! Take a long break ({settings.LongBreakMinutes} min).")
                : ("Focus session complete", $"Nice! Take a short break ({settings.ShortBreakMinutes} min).");
        }

        return ("Break is over", "Ready for the next focus session?");
    }
}
