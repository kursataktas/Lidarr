using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download
{
    public interface IFailedDownloadService
    {
        void MarkAsFailed(int historyId, bool skipRedownload = false);
        void MarkAsFailed(string downloadId, bool skipRedownload = false);
        void Check(TrackedDownload trackedDownload);
        void ProcessFailed(TrackedDownload trackedDownload);
    }

    public class FailedDownloadService : IFailedDownloadService
    {
        private readonly IHistoryService _historyService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IEventAggregator _eventAggregator;

        public FailedDownloadService(IHistoryService historyService,
                                     ITrackedDownloadService trackedDownloadService,
                                     IEventAggregator eventAggregator)
        {
            _historyService = historyService;
            _trackedDownloadService = trackedDownloadService;
            _eventAggregator = eventAggregator;
        }

        public void MarkAsFailed(int historyId, bool skipRedownload = false)
        {
            var history = _historyService.Get(historyId);

            var downloadId = history.DownloadId;
            if (downloadId.IsNullOrWhiteSpace())
            {
                PublishDownloadFailedEvent(new List<EntityHistory> { history }, "Manually marked as failed", skipRedownload: skipRedownload);

                return;
            }

            var grabbedHistory = new List<EntityHistory>();

            // If the history item is a grabbed item (it should be, at least from the UI) add it as the first history item
            if (history.EventType == EntityHistoryEventType.Grabbed)
            {
                grabbedHistory.Add(history);
            }

            // Add any other history items for the download ID then filter out any duplicate history items.
            grabbedHistory.AddRange(_historyService.Find(downloadId, EntityHistoryEventType.Grabbed));
            grabbedHistory = grabbedHistory.DistinctBy(h => h.Id).ToList();

            PublishDownloadFailedEvent(grabbedHistory, "Manually marked as failed");
        }

        public void MarkAsFailed(string downloadId, bool skipRedownload = false)
        {
            var history = _historyService.Find(downloadId, EntityHistoryEventType.Grabbed);

            if (history.Any())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);

                PublishDownloadFailedEvent(history, "Manually marked as failed", trackedDownload, skipRedownload: skipRedownload);
            }
        }

        public void Check(TrackedDownload trackedDownload)
        {
            // Only process tracked downloads that are still downloading
            if (trackedDownload.State != TrackedDownloadState.Downloading)
            {
                return;
            }

            if (trackedDownload.DownloadItem.IsEncrypted ||
                trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed)
            {
                var grabbedItems = _historyService
                                   .Find(trackedDownload.DownloadItem.DownloadId, EntityHistoryEventType.Grabbed)
                                   .ToList();

                if (grabbedItems.Empty())
                {
                    trackedDownload.Warn("Download wasn't grabbed by Lidarr, skipping");
                    return;
                }

                trackedDownload.State = TrackedDownloadState.DownloadFailedPending;
            }
        }

        public void ProcessFailed(TrackedDownload trackedDownload)
        {
            if (trackedDownload.State != TrackedDownloadState.DownloadFailedPending)
            {
                return;
            }

            var grabbedItems = _historyService
                               .Find(trackedDownload.DownloadItem.DownloadId, EntityHistoryEventType.Grabbed)
                               .ToList();

            if (grabbedItems.Empty())
            {
                return;
            }

            var failure = "Failed download detected";

            if (trackedDownload.DownloadItem.IsEncrypted)
            {
                failure = "Encrypted download detected";
            }
            else if (trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed && trackedDownload.DownloadItem.Message.IsNotNullOrWhiteSpace())
            {
                failure = trackedDownload.DownloadItem.Message;
            }

            trackedDownload.State = TrackedDownloadState.DownloadFailed;
            PublishDownloadFailedEvent(grabbedItems, failure, trackedDownload);
        }

        private void PublishDownloadFailedEvent(List<EntityHistory> historyItems, string message, TrackedDownload trackedDownload = null, bool skipRedownload = false)
        {
            var historyItem = historyItems.Last();
            Enum.TryParse(historyItem.Data.GetValueOrDefault(EntityHistory.RELEASE_SOURCE, ReleaseSourceType.Unknown.ToString()), out ReleaseSourceType releaseSource);

            var downloadFailedEvent = new DownloadFailedEvent
            {
                ArtistId = historyItem.ArtistId,
                AlbumIds = historyItems.Select(h => h.AlbumId).Distinct().ToList(),
                Quality = historyItem.Quality,
                SourceTitle = historyItem.SourceTitle,
                DownloadClient = historyItem.Data.GetValueOrDefault(EntityHistory.DOWNLOAD_CLIENT),
                DownloadId = historyItem.DownloadId,
                Message = message,
                Data = historyItem.Data,
                TrackedDownload = trackedDownload,
                SkipRedownload = skipRedownload,
                ReleaseSource = releaseSource
            };

            _eventAggregator.PublishEvent(downloadFailedEvent);
        }
    }
}
