#if WINDOWS
using Lyrava.Sources.YouTube;

namespace Lyrava.App.Windows;

/// <summary>Windows M1 shell joining search, queue and the existing bounded media probe.</summary>
public sealed class WindowsRootPage : TabbedPage
{
    public WindowsRootPage(
        WindowsSearchPage search,
        WindowsQueuePage queuePage,
        MediaProbePage probe,
        WindowsQueueCoordinator queue)
    {
        Title = "Lyrava";
        Children.Add(search);
        Children.Add(queuePage);
        Children.Add(probe);
        search.ResultSelected += (_, args) =>
        {
            var added = queue.TryEnqueue(new SourceTrack(args.Result.VideoId, args.Result.Title));
            if (!added)
            {
                queuePage.Dispatcher.Dispatch(() => queuePage.SetMessage("Already queued or queue is full."));
            }
        };
    }
}
#endif
