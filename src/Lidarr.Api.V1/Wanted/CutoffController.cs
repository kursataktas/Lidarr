using Lidarr.Api.V1.Albums;
using Lidarr.Http;
using Lidarr.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ArtistStats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.SignalR;

namespace Lidarr.Api.V1.Wanted
{
    [V1ApiController("wanted/cutoff")]
    public class CutoffController : AlbumControllerWithSignalR
    {
        private readonly IAlbumCutoffService _albumCutoffService;

        public CutoffController(IAlbumCutoffService albumCutoffService,
                            IAlbumService albumService,
                            IArtistStatisticsService artistStatisticsService,
                            IMapCoversToLocal coverMapper,
                            IUpgradableSpecification upgradableSpecification,
                            IBroadcastSignalRMessage signalRBroadcaster)
        : base(albumService, artistStatisticsService, coverMapper, upgradableSpecification, signalRBroadcaster)
        {
            _albumCutoffService = albumCutoffService;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<AlbumResource> GetCutoffUnmetAlbums([FromQuery] PagingRequestResource paging, bool includeArtist = false, bool monitored = true)
        {
            var pagingResource = new PagingResource<AlbumResource>(paging);
            var pagingSpec = new PagingSpec<Album>
            {
                Page = pagingResource.Page,
                PageSize = pagingResource.PageSize,
                SortKey = pagingResource.SortKey,
                SortDirection = pagingResource.SortDirection
            };

            if (monitored)
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == true && v.Artist.Value.Monitored == true);
            }
            else
            {
                pagingSpec.FilterExpressions.Add(v => v.Monitored == false || v.Artist.Value.Monitored == false);
            }

            return pagingSpec.ApplyToPage(_albumCutoffService.AlbumsWhereCutoffUnmet, v => MapToResource(v, includeArtist));
        }
    }
}
