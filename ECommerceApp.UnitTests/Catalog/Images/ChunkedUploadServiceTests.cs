using AwesomeAssertions;
using ECommerceApp.Application.Catalog.Images.ChunkedUpload;
using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Domain.Catalog.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Images
{
    public class ChunkedUploadServiceTests : IDisposable
    {
        private const string UserId = "user-1";
        private const string OtherUserId = "user-2";
        private const int ItemId = 7;
        private const int ChunkSize = ChunkedUploadLimits.ChunkSizeBytes;

        private readonly string _sandbox;
        private readonly string _tempRoot;
        private readonly Mock<IImageService> _imageService = new();
        private readonly Mock<IProductRepository> _productRepository = new();
        private readonly List<(string FileName, byte[] Content)> _savedUploads = new();

        public ChunkedUploadServiceTests()
        {
            _sandbox = Path.Combine(Path.GetTempPath(), "chunked-upload-tests-" + Guid.NewGuid().ToString("N"));
            _tempRoot = Path.Combine(_sandbox, "Temp");
            SetupExistingProduct(ItemId);
            SetupImageServiceCapturingUploads();
        }

        public void Dispose()
        {
            if (Directory.Exists(_sandbox))
            {
                Directory.Delete(_sandbox, recursive: true);
            }
        }

        // ---------- init: file name ----------

        [Theory]
        [InlineData("..\\..\\evil.jpg", "evil.jpg")]
        [InlineData("../../evil.jpg", "evil.jpg")]
        [InlineData("C:\\evil.jpg", "evil.jpg")]
        [InlineData("/etc/passwd.jpg", "passwd.jpg")]
        [InlineData("Photo.JPG", "Photo.jpg")]
        public async Task InitUpload_PathLikeFileName_ShouldUseOnlySanitizedLeafName(string clientName, string expectedName)
        {
            var service = CreateService();
            var content = ValidJpeg(1000);

            var session = await service.InitUploadAsync(UserId, NewInit(clientName, content.Length));
            await UploadAllChunksAsync(service, UserId, session, content);

            _savedUploads.Should().ContainSingle().Which.FileName.Should().Be(expectedName);
            Directory.GetFileSystemEntries(_sandbox).Should().ContainSingle().Which.Should().Be(_tempRoot);
            Directory.GetFileSystemEntries(_tempRoot).Should().BeEmpty();
        }

        [Theory]
        [InlineData("a\0b.jpg")]
        [InlineData("evil.jpg.")]
        [InlineData("evil.jpg ")]
        [InlineData("CON.jpg")]
        [InlineData("nul.jpg")]
        [InlineData("lpt1.png")]
        [InlineData("x:evil.jpg")]
        [InlineData("evil.exe")]
        [InlineData("evil")]
        [InlineData(".jpg")]
        [InlineData("")]
        [InlineData(null)]
        public async Task InitUpload_InvalidFileName_ShouldBeRejectedAndLeaveNothingOnDisk(string clientName)
        {
            var service = CreateService();

            Func<Task> act = () => service.InitUploadAsync(UserId, NewInit(clientName, 1000));

            await act.Should().ThrowAsync<BusinessException>();
            Directory.Exists(_tempRoot).Should().BeFalse();
        }

        [Fact]
        public async Task InitUpload_VeryLongFileName_ShouldBeRejected()
        {
            var service = CreateService();

            Func<Task> act = () => service.InitUploadAsync(UserId, NewInit(new string('a', 300) + ".jpg", 1000));

            await act.Should().ThrowAsync<BusinessException>();
        }

        // ---------- init: size / item / session cap ----------

        [Theory]
        [InlineData(0L)]
        [InlineData(-1L)]
        [InlineData(10L * 1024 * 1024 + 1)]
        [InlineData(long.MaxValue)]
        public async Task InitUpload_SizeOutOfRange_ShouldBeRejected(long size)
        {
            var service = CreateService();

            Func<Task> act = () => service.InitUploadAsync(UserId, NewInit("a.jpg", size));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task InitUpload_MaxAllowedSize_ShouldBeAcceptedWithCappedChunkCount()
        {
            var service = CreateService();

            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", 10L * 1024 * 1024));

            session.TotalChunks.Should().Be(10);
            session.ChunkIds.Should().Equal(Enumerable.Range(1, 10));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task InitUpload_NonPositiveItemId_ShouldBeRejected(int itemId)
        {
            var service = CreateService();

            Func<Task> act = () => service.InitUploadAsync(UserId, NewInit("a.jpg", 1000, itemId));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task InitUpload_UnknownItem_ShouldBeRejected()
        {
            SetupMissingProduct(404);
            var service = CreateService();

            Func<Task> act = () => service.InitUploadAsync(UserId, NewInit("a.jpg", 1000, 404));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Fact]
        public async Task InitUpload_OverPerUserSessionCap_ShouldBeRejectedOnlyForThatUser()
        {
            var service = CreateService(new ChunkedUploadOptions { MaxOpenSessionsPerUser = 2 });
            await service.InitUploadAsync(UserId, NewInit("a.jpg", 1000));
            await service.InitUploadAsync(UserId, NewInit("b.jpg", 1000));

            Func<Task> overCap = () => service.InitUploadAsync(UserId, NewInit("c.jpg", 1000));
            Func<Task> otherUser = () => service.InitUploadAsync(OtherUserId, NewInit("c.jpg", 1000));

            await overCap.Should().ThrowAsync<BusinessException>();
            await otherUser.Should().NotThrowAsync();
        }

        // ---------- chunk integrity ----------

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public async Task UploadChunk_ChunkIdOutsideRange_ShouldBeRejected(int chunkId)
        {
            var service = CreateService();
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", ChunkSize + 10));

            Func<Task> act = () => service.UploadChunkAsync(UserId, NewChunk(session.SessionId, chunkId, new byte[ChunkSize]));

            await act.Should().ThrowAsync<BusinessException>();
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(1, ChunkSize - 1)]
        [InlineData(1, ChunkSize + 1)]
        [InlineData(2, 0)]
        [InlineData(2, 9)]
        [InlineData(2, 11)]
        public async Task UploadChunk_WrongChunkLength_ShouldBeRejectedAndNotCounted(int chunkId, int length)
        {
            var service = CreateService();
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", ChunkSize + 10));

            Func<Task> act = () => service.UploadChunkAsync(UserId, NewChunk(session.SessionId, chunkId, new byte[length]));

            await act.Should().ThrowAsync<BusinessException>();
            service.GetStatus(UserId, session.SessionId).ReceivedChunkIds.Should().BeEmpty();
        }

        [Fact]
        public async Task UploadChunk_DuplicateChunk_ShouldBeIdempotentAndOverwrite()
        {
            var service = CreateService();
            var content = ValidJpeg(ChunkSize + 10);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", content.Length));
            var firstChunk = content.Take(ChunkSize).ToArray();

            var first = await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 1, new byte[ChunkSize]));
            var resend = await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 1, firstChunk));

            first.ReceivedCount.Should().Be(1);
            resend.ReceivedCount.Should().Be(1);
            var last = await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 2, content.Skip(ChunkSize).ToArray()));
            last.Complete.Should().BeTrue();
            _savedUploads.Should().ContainSingle().Which.Content.Should().Equal(content);
        }

        [Fact]
        public async Task UploadChunk_OutOfOrderChunks_ShouldAssembleInChunkOrder()
        {
            var service = CreateService();
            var content = ValidJpeg(2 * ChunkSize + 500);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", content.Length));

            foreach (var chunkId in new[] { 3, 1, 2 })
            {
                await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, chunkId, Slice(content, chunkId)));
            }

            _savedUploads.Should().ContainSingle().Which.Content.Should().Equal(content);
            Directory.GetFileSystemEntries(_tempRoot).Should().BeEmpty();
        }

        [Fact]
        public async Task UploadChunk_AllChunks_ShouldReturnImageIdAndRemoveSession()
        {
            var service = CreateService();
            var content = ValidJpeg(2000);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", content.Length, ItemId));

            var response = await UploadAllChunksAsync(service, UserId, session, content);

            response.Complete.Should().BeTrue();
            response.ImageId.Should().Be(42);
            service.GetStatus(UserId, session.SessionId).Should().BeNull();
        }

        [Fact]
        public async Task UploadChunk_ImageServiceRejectsContent_ShouldPropagateAndDeleteSessionAndTempFiles()
        {
            _imageService.Reset();
            _imageService.Setup(s => s.AddImages(It.IsAny<AddImagesPOCO>())).ThrowsAsync(new BusinessException("not an image"));
            var service = CreateService();
            var content = ValidJpeg(2000);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", content.Length));

            Func<Task> act = () => UploadAllChunksAsync(service, UserId, session, content);

            await act.Should().ThrowAsync<BusinessException>();
            service.GetStatus(UserId, session.SessionId).Should().BeNull();
            Directory.GetFileSystemEntries(_tempRoot).Should().BeEmpty();
        }

        [Fact]
        public async Task UploadChunk_ConcurrentLastChunks_ShouldSaveExactlyOneImage()
        {
            _imageService.Reset();
            SetupImageServiceCapturingUploads(delayMs: 30);
            var service = CreateService();
            var content = ValidJpeg(3 * ChunkSize - 5);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", content.Length));
            await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 1, Slice(content, 1)));

            var results = await Task.WhenAll(
                service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 2, Slice(content, 2))),
                service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 3, Slice(content, 3))),
                service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 3, Slice(content, 3))));

            _savedUploads.Should().ContainSingle();
            results.Count(r => r != null && r.Complete).Should().BeGreaterThanOrEqualTo(1);
        }

        // ---------- ownership / no info leak ----------

        [Fact]
        public async Task UploadChunk_SessionOfAnotherUser_ShouldReturnSameAsUnknownSession()
        {
            var service = CreateService();
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", 1000));
            var chunk = new byte[1000];

            var foreign = await service.UploadChunkAsync(OtherUserId, NewChunk(session.SessionId, 1, chunk));
            var unknown = await service.UploadChunkAsync(OtherUserId, NewChunk(Guid.NewGuid(), 1, chunk));
            var anonymous = await service.UploadChunkAsync(null, NewChunk(session.SessionId, 1, chunk));

            foreign.Should().BeNull();
            unknown.Should().BeNull();
            anonymous.Should().BeNull();
            service.GetStatus(UserId, session.SessionId).ReceivedChunkIds.Should().BeEmpty();
        }

        [Fact]
        public async Task GetStatus_SessionOfAnotherUser_ShouldReturnNull()
        {
            var service = CreateService();
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", 1000));

            service.GetStatus(OtherUserId, session.SessionId).Should().BeNull();
            service.GetStatus(OtherUserId, Guid.NewGuid()).Should().BeNull();
            service.GetStatus(UserId, session.SessionId).Should().NotBeNull();
        }

        // ---------- resume ----------

        [Fact]
        public async Task Resume_AfterRestart_ShouldReportReceivedChunksAndCompleteUpload()
        {
            var content = ValidJpeg(3 * ChunkSize - 100);
            var beforeRestart = CreateService();
            var session = await beforeRestart.InitUploadAsync(UserId, NewInit("a.jpg", content.Length, ItemId));
            await beforeRestart.UploadChunkAsync(UserId, NewChunk(session.SessionId, 1, Slice(content, 1)));
            await beforeRestart.UploadChunkAsync(UserId, NewChunk(session.SessionId, 3, Slice(content, 3)));

            var afterRestart = CreateService();
            var status = afterRestart.GetStatus(UserId, session.SessionId);
            var response = await afterRestart.UploadChunkAsync(UserId, NewChunk(session.SessionId, 2, Slice(content, 2)));

            status.ReceivedChunkIds.Should().Equal(1, 3);
            status.TotalChunks.Should().Be(3);
            status.ChunkSize.Should().Be(ChunkSize);
            status.Complete.Should().BeFalse();
            response.Complete.Should().BeTrue();
            _savedUploads.Should().ContainSingle().Which.Content.Should().Equal(content);
        }

        [Fact]
        public async Task Resume_AfterRestart_ShouldNotExposeSessionToAnotherUser()
        {
            var first = CreateService();
            var session = await first.InitUploadAsync(UserId, NewInit("a.jpg", 1000));

            var afterRestart = CreateService();

            afterRestart.GetStatus(OtherUserId, session.SessionId).Should().BeNull();
            afterRestart.GetStatus(UserId, session.SessionId).Should().NotBeNull();
        }

        [Fact]
        public async Task Resume_PartFileWithWrongSize_ShouldNotCountAsReceived()
        {
            var service = CreateService();
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", ChunkSize + 10));
            await service.UploadChunkAsync(UserId, NewChunk(session.SessionId, 1, new byte[ChunkSize]));
            File.WriteAllBytes(Path.Combine(_tempRoot, session.SessionId.ToString("D"), "2.part"), new byte[3]);

            var afterRestart = CreateService();

            afterRestart.GetStatus(UserId, session.SessionId).ReceivedChunkIds.Should().Equal(1);
        }

        [Fact]
        public async Task Resume_AfterRestartInitUploadCap_ShouldCountSessionsFromDisk()
        {
            var options = new ChunkedUploadOptions { MaxOpenSessionsPerUser = 1 };
            await CreateService(options).InitUploadAsync(UserId, NewInit("a.jpg", 1000));
            var afterRestart = CreateService(options);

            Func<Task> act = () => afterRestart.InitUploadAsync(UserId, NewInit("b.jpg", 1000));

            await act.Should().ThrowAsync<BusinessException>();
        }

        // ---------- cleanup ----------

        [Fact]
        public async Task Cleanup_ExpiredSessions_ShouldBeRemovedFromDiskAndMemoryAndActiveOnesKept()
        {
            var options = new ChunkedUploadOptions { SessionTtl = TimeSpan.FromHours(24) };
            var store = CreateStore(options);
            var service = CreateService(options, store);
            var expired = await service.InitUploadAsync(UserId, NewInit("a.jpg", ChunkSize + 10));
            var active = await service.InitUploadAsync(UserId, NewInit("b.jpg", 1000));
            await service.UploadChunkAsync(UserId, NewChunk(expired.SessionId, 1, new byte[ChunkSize]));
            BackdateSession(expired.SessionId, TimeSpan.FromHours(25));
            var task = CreateCleanupTask(store, options);
            var context = new ECommerceApp.Application.Supporting.TimeManagement.JobExecutionContext(null, Guid.NewGuid().ToString());

            await task.ExecuteAsync(context, default);

            Directory.Exists(Path.Combine(_tempRoot, expired.SessionId.ToString("D"))).Should().BeFalse();
            service.GetStatus(UserId, expired.SessionId).Should().BeNull();
            service.GetStatus(UserId, active.SessionId).Should().NotBeNull();
            context.Outcome.Should().BeOfType<ECommerceApp.Application.Supporting.TimeManagement.Models.JobOutcome.Success>();
        }

        [Fact]
        public async Task Cleanup_Disabled_ShouldRemoveNothing()
        {
            var options = new ChunkedUploadOptions { Enabled = false };
            var store = CreateStore(options);
            var service = CreateService(options, store);
            var session = await service.InitUploadAsync(UserId, NewInit("a.jpg", 1000));
            BackdateSession(session.SessionId, TimeSpan.FromDays(10));
            var task = CreateCleanupTask(store, options);

            await task.ExecuteAsync(new ECommerceApp.Application.Supporting.TimeManagement.JobExecutionContext(null, "x"), default);

            Directory.Exists(Path.Combine(_tempRoot, session.SessionId.ToString("D"))).Should().BeTrue();
        }

        // ---------- helpers ----------

        private ChunkedUploadService CreateService(ChunkedUploadOptions options = null, UploadSessionStore store = null)
        {
            var wrapped = Options.Create(WithTempRoot(options));
            return new ChunkedUploadService(store ?? new UploadSessionStore(wrapped), _imageService.Object, _productRepository.Object, wrapped);
        }

        private UploadSessionStore CreateStore(ChunkedUploadOptions options = null)
            => new(Options.Create(WithTempRoot(options)));

        private ChunkedUploadCleanupTask CreateCleanupTask(UploadSessionStore store, ChunkedUploadOptions options)
            => new(store, Options.Create(WithTempRoot(options)));

        private ChunkedUploadOptions WithTempRoot(ChunkedUploadOptions options)
        {
            options ??= new ChunkedUploadOptions();
            return new ChunkedUploadOptions
            {
                Enabled = options.Enabled,
                MaxOpenSessionsPerUser = options.MaxOpenSessionsPerUser,
                SessionTtl = options.SessionTtl,
                Schedule = options.Schedule,
                TempPath = _tempRoot
            };
        }

        private void SetupExistingProduct(int itemId)
            => _productRepository.Setup(r => r.ExistsByIdAsync(new ProductId(itemId))).ReturnsAsync(true);

        private void SetupMissingProduct(int itemId)
            => _productRepository.Setup(r => r.ExistsByIdAsync(new ProductId(itemId))).ReturnsAsync(false);

        private void SetupImageServiceCapturingUploads(int delayMs = 0)
        {
            _imageService.Setup(s => s.AddImages(It.IsAny<AddImagesPOCO>()))
                .Returns(async (AddImagesPOCO poco) =>
                {
                    var file = poco.Files.Single();
                    using var ms = new MemoryStream();
                    await using (var stream = file.OpenReadStream())
                    {
                        await stream.CopyToAsync(ms);
                    }

                    lock (_savedUploads)
                    {
                        _savedUploads.Add((file.FileName, ms.ToArray()));
                    }

                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs);
                    }

                    return new List<int> { 42 };
                });
        }

        private void BackdateSession(Guid sessionId, TimeSpan age)
        {
            var dir = Path.Combine(_tempRoot, sessionId.ToString("D"));
            var past = DateTime.UtcNow - age;
            foreach (var file in Directory.GetFiles(dir))
            {
                File.SetLastWriteTimeUtc(file, past);
            }

            Directory.SetLastWriteTimeUtc(dir, past);
        }

        private static InitUploadRequest NewInit(string name, long size, int? itemId = ItemId)
            => new() { FileName = name, FileSizeBytes = size, ItemId = itemId };

        private static UploadChunkRequest NewChunk(Guid sessionId, int chunkId, byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            return new UploadChunkRequest
            {
                SessionId = sessionId,
                ChunkId = chunkId,
                Chunk = new FormFile(stream, 0, bytes.Length, "chunk", "blob") { Headers = new HeaderDictionary() }
            };
        }

        private static byte[] Slice(byte[] content, int chunkId)
            => content.Skip((chunkId - 1) * ChunkSize).Take(ChunkSize).ToArray();

        private static async Task<UploadChunkResponse> UploadAllChunksAsync(IChunkedUploadService service, string userId, InitUploadResponse session, byte[] content)
        {
            UploadChunkResponse last = null;
            foreach (var chunkId in session.ChunkIds)
            {
                last = await service.UploadChunkAsync(userId, NewChunk(session.SessionId, chunkId, Slice(content, chunkId)));
            }

            return last;
        }

        private static byte[] ValidJpeg(int length)
        {
            var bytes = new byte[length];
            new Random(1).NextBytes(bytes);
            bytes[0] = 0xFF;
            bytes[1] = 0xD8;
            bytes[2] = 0xFF;
            bytes[^2] = 0xFF;
            bytes[^1] = 0xD9;
            return bytes;
        }
    }
}
