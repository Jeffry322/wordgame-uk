namespace WordGameUk.Components.Layout;

public sealed class RoomTopBarState
{
    public string Title { get; private set; } = string.Empty;
    public string Subtitle { get; private set; } = string.Empty;
    public string Meta { get; private set; } = string.Empty;
    public bool ShowLeaveButton { get; private set; }
    public Func<Task>? LeaveAction { get; private set; }

    public event Action? Changed;

    public void Update(string title, string subtitle, string meta, bool showLeaveButton, Func<Task>? leaveAction)
    {
        Title = title;
        Subtitle = subtitle;
        Meta = meta;
        ShowLeaveButton = showLeaveButton;
        LeaveAction = leaveAction;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Meta = string.Empty;
        ShowLeaveButton = false;
        LeaveAction = null;
        Changed?.Invoke();
    }
}
