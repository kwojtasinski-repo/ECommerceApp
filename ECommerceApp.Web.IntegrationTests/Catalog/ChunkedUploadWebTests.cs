using ECommerceApp.Application.Catalog.Images.ChunkedUpload;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Web.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SkiaSharp;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Catalog
{
    /// <summary>
    /// HTTP contract of the Web (cookie-auth) "Classic" chunked upload endpoints:
    /// <c>/Catalog/Image/InitUpload</c>, <c>/UploadChunk</c>, <c>/UploadStatus</c>.
    /// Detailed validation rules are covered by unit and API-level tests; these tests pin the Web wiring
    /// (authorization, status codes, user-bound sessions).
    /// </summary>
    public class ChunkedUploadWebTests
        : WebTestBase<TusUploadTestFactory>, IClassFixture<TusUploadTestFactory>
    {
        private const string HostileMarker = "HOSTILE-MARKER-7f3a91";

        private static readonly byte[] MinJpegBytes = CreateJpeg();

        private int _itemId;

        public ChunkedUploadWebTests(TusUploadTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _itemId = await _factory.CreateTestProductAsync();
        }

        [Theory]
        [InlineData("GET", "/Catalog/Image/UploadStatus?sessionId=00000000-0000-0000-0000-000000000001")]
        [InlineData("POST", "/Catalog/Image/InitUpload")]
        [InlineData("POST", "/Catalog/Image/UploadChunk")]
        public async Task ChunkedEndpoints_Anonymous_ShouldRedirectToLogin(string method, string url)
        {
            var client = ((Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Startup>)_factory)
                .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

            var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Found);
            response.Headers.Location?.ToString().ShouldContain("Login");
        }

        [Fact]
        public async Task UploadStatus_UnknownSession_ShouldReturnNotFound()
        {
            var client = await CreateAuthenticatedClientAsync();

            var response = await client.GetAsync($"/Catalog/Image/UploadStatus?sessionId={Guid.NewGuid()}", CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UploadChunk_UnknownSession_ShouldReturnNotFound()
        {
            var client = await CreateAuthenticatedClientAsync();

            var response = await PostChunkAsync(client, Guid.NewGuid(), 1, new byte[10]);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task InitUpload_TraversalOnlyExtensionlessName_ShouldReturnBadRequest()
        {
            var client = await CreateAuthenticatedClientAsync();

            var response = await InitUploadAsync(client, "..\\..\\evil", 100, _itemId);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task InitUpload_UnknownItem_ShouldReturnBadRequest()
        {
            var client = await CreateAuthenticatedClientAsync();

            var response = await InitUploadAsync(client, "a.jpg", 100, 987654321);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ChunkedUpload_ValidSingleChunkJpeg_ShouldCompleteAndSessionBeGone()
        {
            var client = await CreateAuthenticatedClientAsync();
            var init = await InitUploadAsync(client, "..\\..\\photo.jpg", MinJpegBytes.Length, _itemId);
            init.StatusCode.ShouldBe(HttpStatusCode.OK);
            var session = JsonSerializer.Deserialize<InitUploadResponse>(
                await init.Content.ReadAsStringAsync(CancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var chunk = await PostChunkAsync(client, session.SessionId, 1, MinJpegBytes);
            var status = await client.GetAsync($"/Catalog/Image/UploadStatus?sessionId={session.SessionId}", CancellationToken);

            chunk.StatusCode.ShouldBe(HttpStatusCode.OK);
            status.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ChunkedUpload_JpegWithCommentSegmentCarryingMarker_ShouldNotStoreMarker()
        {
            var client = await CreateAuthenticatedClientAsync();
            var fileStore = (TusFakeFileStore)_factory.Services.GetRequiredService<IFileStore>();
            var polluted = WithCommentSegment(MinJpegBytes, HostileMarker);
            var init = await InitUploadAsync(client, "polluted.jpg", polluted.Length, _itemId);
            var session = JsonSerializer.Deserialize<InitUploadResponse>(
                await init.Content.ReadAsStringAsync(CancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var chunk = await PostChunkAsync(client, session.SessionId, 1, polluted);

            chunk.StatusCode.ShouldBe(HttpStatusCode.OK);
            fileStore.WrittenFiles.ShouldNotBeEmpty();
            fileStore.WrittenFiles.Any(file => file.AsSpan().IndexOf(Encoding.ASCII.GetBytes(HostileMarker)) >= 0).ShouldBeFalse();
        }

        private static byte[] CreateJpeg()
        {
            using var bitmap = new SKBitmap(16, 16);
            bitmap.Erase(SKColors.Red);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            return data.ToArray();
        }

        private static byte[] WithCommentSegment(byte[] jpeg, string text)
        {
            var payload = Encoding.ASCII.GetBytes(text);
            var length = payload.Length + 2;
            var result = new System.Collections.Generic.List<byte>(jpeg[..2]) { 0xFF, 0xFE, (byte)(length >> 8), (byte)length };
            result.AddRange(payload);
            result.AddRange(jpeg[2..]);
            return result.ToArray();
        }

        private Task<HttpResponseMessage> InitUploadAsync(HttpClient client, string fileName, long size, int itemId)
            => client.PostAsync(
                "/Catalog/Image/InitUpload",
                new StringContent(
                    JsonSerializer.Serialize(new InitUploadRequest { FileName = fileName, FileSizeBytes = size, ItemId = itemId }),
                    Encoding.UTF8,
                    "application/json"),
                CancellationToken);

        private Task<HttpResponseMessage> PostChunkAsync(HttpClient client, Guid sessionId, int chunkId, byte[] bytes)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(sessionId.ToString()), "sessionId" },
                { new StringContent(chunkId.ToString()), "chunkId" },
                { new ByteArrayContent(bytes), "chunk", "blob" }
            };
            return client.PostAsync("/Catalog/Image/UploadChunk", form, CancellationToken);
        }
    }
}
