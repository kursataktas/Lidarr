using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Music;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class QueueSpecificationFixture : CoreTest<QueueSpecification>
    {
        private Artist _artist;
        private Album _album;
        private RemoteAlbum _remoteAlbum;

        private Artist _otherArtist;
        private Album _otherAlbum;

        private ReleaseInfo _releaseInfo;

        [SetUp]
        public void Setup()
        {
            Mocker.Resolve<UpgradableSpecification>();

            CustomFormatsTestHelpers.GivenCustomFormats();

            _artist = Builder<Artist>.CreateNew()
                                     .With(e => e.QualityProfile = new QualityProfile
                                     {
                                         UpgradeAllowed = true,
                                         Items = Qualities.QualityFixture.GetDefaultQualities(),
                                         FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(),
                                         MinFormatScore = 0
                                     })
                                     .Build();

            _album = Builder<Album>.CreateNew()
                                       .With(e => e.ArtistId = _artist.Id)
                                       .Build();

            _otherArtist = Builder<Artist>.CreateNew()
                                          .With(s => s.Id = 2)
                                          .Build();

            _otherAlbum = Builder<Album>.CreateNew()
                                            .With(e => e.ArtistId = _otherArtist.Id)
                                            .With(e => e.Id = 2)
                                            .Build();

            _releaseInfo = Builder<ReleaseInfo>.CreateNew()
                                   .Build();

            _remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                   .With(r => r.Artist = _artist)
                                                   .With(r => r.Albums = new List<Album> { _album })
                                                   .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new QualityModel(Quality.MP3_256) })
                                                   .With(r => r.CustomFormats = new List<CustomFormat>())
                                                   .Build();

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<RemoteAlbum>(), It.IsAny<long>()))
                  .Returns(new List<CustomFormat>());
        }

        private void GivenEmptyQueue()
        {
            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(new List<Queue.Queue>());
        }

        private void GivenQueueFormats(List<CustomFormat> formats)
        {
            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(x => x.ParseCustomFormat(It.IsAny<RemoteAlbum>(), It.IsAny<long>()))
                  .Returns(formats);
        }

        private void GivenQueue(IEnumerable<RemoteAlbum> remoteAlbums, TrackedDownloadState trackedDownloadState = TrackedDownloadState.Downloading)
        {
            var queue = remoteAlbums.Select(remoteAlbum => new Queue.Queue
            {
                RemoteAlbum = remoteAlbum,
                TrackedDownloadState = trackedDownloadState
            });

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(queue.ToList());
        }

        [Test]
        public void should_return_true_when_queue_is_empty()
        {
            GivenEmptyQueue();
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_artist_doesnt_match()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                       .With(r => r.Artist = _otherArtist)
                                                       .With(r => r.Albums = new List<Album> { _album })
                                                       .With(r => r.Release = _releaseInfo)
                                                       .With(r => r.CustomFormats = new List<CustomFormat>())
                                                       .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_everything_is_the_same()
        {
            _artist.QualityProfile.Value.Cutoff = Quality.FLAC.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(r => r.Artist = _artist)
                .With(r => r.Albums = new List<Album> { _album })
                .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new QualityModel(Quality.MP3_256)
                })
                .With(r => r.CustomFormats = new List<CustomFormat>())
                .With(r => r.Release = _releaseInfo)
                .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });

            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_quality_in_queue_is_lower()
        {
            _artist.QualityProfile.Value.Cutoff = Quality.MP3_320.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_album_doesnt_match()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_qualities_are_the_same_with_higher_custom_format_score()
        {
            _remoteAlbum.CustomFormats = new List<CustomFormat> { new CustomFormat("My Format", new ReleaseTitleSpecification { Value = "MP3" }) { Id = 1 } };

            var lowFormat = new List<CustomFormat> { new CustomFormat("Bad Format", new ReleaseTitleSpecification { Value = "MP3" }) { Id = 2 } };

            CustomFormatsTestHelpers.GivenCustomFormats(_remoteAlbum.CustomFormats.First(), lowFormat.First());

            _artist.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems("My Format");

            GivenQueueFormats(lowFormat);

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(r => r.Artist = _artist)
                .With(r => r.Albums = new List<Album> { _album })
                .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new QualityModel(Quality.MP3_256)
                })
                .With(r => r.Release = _releaseInfo)
                .With(r => r.CustomFormats = lowFormat)
                .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_when_qualities_are_the_same()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_quality_in_queue_is_better()
        {
            _artist.QualityProfile.Value.Cutoff = Quality.FLAC.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_matching_multi_album_is_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album, _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_album_has_one_album_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_part_album_is_already_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album, _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320)
                                                      })
                                                      .With(r => r.Release = _releaseInfo)
                                                      .With(r => r.CustomFormats = new List<CustomFormat>())
                                                      .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_part_album_has_two_albums_in_queue()
        {
            var remoteAlbums = Builder<RemoteAlbum>.CreateListOfSize(2)
                                                       .All()
                                                       .With(r => r.Artist = _artist)
                                                       .With(r => r.CustomFormats = new List<CustomFormat>())
                                                       .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                       {
                                                           Quality = new QualityModel(Quality.MP3_320)
                                                       })
                                                       .With(r => r.Release = _releaseInfo)
                                                       .TheFirst(1)
                                                       .With(r => r.Albums = new List<Album> { _album })
                                                       .TheNext(1)
                                                       .With(r => r.Albums = new List<Album> { _otherAlbum })
                                                       .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);
            GivenQueue(remoteAlbums);
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_quality_is_better_and_upgrade_allowed_is_false_for_quality_profile()
        {
            _artist.QualityProfile.Value.Cutoff = Quality.FLAC.Id;
            _artist.QualityProfile.Value.UpgradeAllowed = false;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(r => r.Artist = _artist)
                .With(r => r.Albums = new List<Album> { _album })
                .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new QualityModel(Quality.FLAC)
                })
                .With(r => r.Release = _releaseInfo)
                .With(r => r.CustomFormats = new List<CustomFormat>())
                .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_if_everything_is_the_same_for_failed_pending()
        {
            _artist.QualityProfile.Value.Cutoff = Quality.FLAC.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(r => r.Artist = _artist)
                .With(r => r.Albums = new List<Album> { _album })
                .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new QualityModel(Quality.MP3_008)
                })
                .With(r => r.Release = _releaseInfo)
                .With(r => r.CustomFormats = new List<CustomFormat>())
                .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum }, TrackedDownloadState.DownloadFailedPending);

            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_same_quality_non_proper_in_queue_and_download_propers_is_do_not_upgrade()
        {
            _remoteAlbum.ParsedAlbumInfo.Quality = new QualityModel(Quality.MP3_008, new Revision(2));
            _artist.QualityProfile.Value.Cutoff = _remoteAlbum.ParsedAlbumInfo.Quality.Quality.Id;

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.DownloadPropersAndRepacks)
                .Returns(ProperDownloadTypes.DoNotUpgrade);

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                .With(r => r.Artist = _artist)
                .With(r => r.Albums = new List<Album> { _album })
                .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    Quality = new QualityModel(Quality.MP3_008)
                })
                .With(r => r.Release = _releaseInfo)
                .With(r => r.CustomFormats = new List<CustomFormat>())
                .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });

            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }
    }
}
