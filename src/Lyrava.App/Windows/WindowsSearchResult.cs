#if WINDOWS
namespace Lyrava.App.Windows;

/// <summary>脱敏的搜索展示项；临时媒体地址不进入页面模型。</summary>
public sealed record WindowsSearchResult(string VideoId, string Title);

public sealed class WindowsSearchResultSelectedEventArgs(WindowsSearchResult result) : EventArgs
{
    public WindowsSearchResult Result { get; } = result ?? throw new ArgumentNullException(nameof(result));
}
#endif
