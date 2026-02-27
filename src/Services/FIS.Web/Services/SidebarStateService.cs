namespace FIS.Web.Services;

public class SidebarStateService
{
    private bool _isCollapsed;

    public bool IsCollapsed => _isCollapsed;

    public event Action? Changed;

    public void SetCollapsed(bool collapsed)
    {
        if (_isCollapsed == collapsed)
        {
            return;
        }

        _isCollapsed = collapsed;
        Changed?.Invoke();
    }

    public void Toggle()
    {
        SetCollapsed(!_isCollapsed);
    }
}
