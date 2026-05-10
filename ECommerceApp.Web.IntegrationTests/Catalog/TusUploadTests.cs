using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.IntegrationTests.Catalog
{
    /// <summary>
    /// Integration tests for the TUS resumable upload protocol endpoint (<c>/tus</c>).
    ///
    /// These tests are intentionally RED until the TUS implementation (Phase 1+2) lands.
    /// They define the expected server contract so the implementation can be driven by
    /// failing tests (TDD red → green → refactor).
    ///
    /// TUS protocol summary used here (v1.0.0):
    ///   POST   /tus                    → 201 Created   + Location header
    ///   HEAD   /tus/{uploadId}         → 200 OK        + Upload-Offset + Upload-Length headers
    ///   PATCH  /tus/{uploadId}         → 204 No Content + Upload-Offset header
    ///   POST   /Catalog/Image/CompleteUpload → 200 OK (server-side image commit)
    ///
    /// All authenticated requests use the seeded Administrator account via cookie login.
    /// Anonymous requests must be rejected before any TUS processing occurs.
    ///
    /// References:
    ///   https://tus.io/protocols/resumable-upload.html
    ///   Report C — TUS JS Changes: Detailed Review
    /// </summary>
    public class TusUploadTests
        : WebTestBase<TusUploadTestFactory>, IClassFixture<TusUploadTestFactory>
    {
        // TUS protocol constants
        private const string TusResumableHeader = "Tus-Resumable";
        private const string TusResumableVersion = "1.0.0";
        private const string TusEndpoint = "/tus";

        // Minimal valid JPEG bytes (JFIF header) — enough for the image service to accept.
        private static readonly byte[] MinJpegBytes =
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
            0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
        };

        public TusUploadTests(TusUploadTestFactory factory, ITestOutputHelper output)
            : base(factory, output) { }

        // ─── Auth guard ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Anonymous POST to /tus must be rejected before the TUS middleware touches the request.
        /// A 401 proves the auth middleware runs first.
        /// </summary>
        [Fact]
        public async Task Tus_Post_AnonymousRequest_Returns401()
        {
            var client = CreateClient(); // unauthenticated

            var request = new HttpRequestMessage(HttpMethod.Post, TusEndpoint);
            request.Headers.Add(TusResumableHeader, TusResumableVersion);
            request.Headers.Add("Upload-Length", MinJpegBytes.Length.ToString());

            var response = await client.SendAsync(request, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Anonymous HEAD to /tus/{id} must also return 401.
        /// </summary>
        [Fact]
        public async Task Tus_Head_AnonymousRequest_Returns401()
        {
            var client = CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Head, $"{TusEndpoint}/{Guid.NewGuid()}");
            request.Headers.Add(TusResumableHeader, TusResumableVersion);

            var response = await client.SendAsync(request, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // ─── TUS creation (POST) ─────────────────────────────────────────────────────

        /// <summary>
        /// Authenticated POST with valid Upload-Length → 201 Created + Location header pointing
        /// to the new upload resource. The Location must be an absolute or root-relative URL
        /// under /tus/.
        /// </summary>
        [Fact]
        public async Task Tus_Post_ValidRequest_Returns201WithLocation()
        {
            var client = await CreateAuthenticatedClientAsync();

            var location = await CreateTusUploadAsync(client, fileBytes: MinJpegBytes, itemId: 1);

            location.ShouldNotBeNull();
            location.ToString().ShouldContain("/tus/");
        }

        /// <summary>
        /// POST without Upload-Length header is a malformed TUS request → 400 Bad Request.
        /// tusdotnet validates this before accepting the upload.
        /// </summary>
        [Fact]
        public async Task Tus_Post_MissingUploadLength_Returns400()
        {
            var client = await CreateAuthenticatedClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, TusEndpoint);
            request.Headers.Add(TusResumableHeader, TusResumableVersion);
            // Deliberately omit Upload-Length

            var response = await client.SendAsync(request, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // ─── TUS resume check (HEAD) ─────────────────────────────────────────────────

        /// <summary>
        /// HEAD after creation with no data uploaded must return:
        ///   Upload-Offset: 0
        ///   Upload-Length: &lt;file size&gt;
        ///   Tus-Resumable: 1.0.0
        /// </summary>
        [Fact]
        public async Task Tus_Head_AfterCreation_ReturnsZeroOffset()
        {
            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: MinJpegBytes, itemId: 1);

            var headRequest = new HttpRequestMessage(HttpMethod.Head, location);
            headRequest.Headers.Add(TusResumableHeader, TusResumableVersion);

            var response = await client.SendAsync(headRequest, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.TryGetValues("Upload-Offset", out var offsets).ShouldBeTrue();
            offsets.ShouldContain("0");
            response.Headers.TryGetValues("Upload-Length", out var lengths).ShouldBeTrue();
            lengths.ShouldContain(MinJpegBytes.Length.ToString());
        }

        /// <summary>
        /// HEAD on a non-existent upload ID must return a non-success status.
        /// tusdotnet 2.4.0 returns 400 Bad Request (rather than 404) when the file
        /// is not found in the disk store — either status correctly signals "not found".
        /// </summary>
        [Fact]
        public async Task Tus_Head_UnknownUploadId_ReturnsNonSuccess()
        {
            var client = await CreateAuthenticatedClientAsync();

            var request = new HttpRequestMessage(HttpMethod.Head, $"{TusEndpoint}/{Guid.NewGuid()}");
            request.Headers.Add(TusResumableHeader, TusResumableVersion);

            var response = await client.SendAsync(request, CancellationToken);

            response.IsSuccessStatusCode.ShouldBeFalse(
                $"Expected a non-success status for an unknown upload ID, got {response.StatusCode}");
        }

        // ─── TUS data upload (PATCH) ─────────────────────────────────────────────────

        /// <summary>
        /// PATCH with full file content in one chunk → 204 No Content.
        /// Upload-Offset response header must equal the number of bytes uploaded.
        /// </summary>
        [Fact]
        public async Task Tus_Patch_SingleChunk_Returns204AndCorrectOffset()
        {
            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: MinJpegBytes, itemId: 1);

            var patchResponse = await PatchTusChunkAsync(client, location, offset: 0, data: MinJpegBytes);

            patchResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            patchResponse.Headers.TryGetValues("Upload-Offset", out var offsets).ShouldBeTrue();
            offsets.ShouldContain(MinJpegBytes.Length.ToString());
        }

        /// <summary>
        /// PATCH with wrong Upload-Offset (conflict) → 409 Conflict.
        /// tusdotnet enforces sequential, non-overlapping writes.
        /// </summary>
        [Fact]
        public async Task Tus_Patch_WrongOffset_Returns409()
        {
            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: MinJpegBytes, itemId: 1);

            // Send with offset=5 before uploading the first 5 bytes → offset mismatch
            var response = await PatchTusChunkAsync(client, location, offset: 5, data: MinJpegBytes);

            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        /// <summary>
        /// Two sequential PATCH requests (simulating a two-chunk upload) must both succeed
        /// and the final Upload-Offset must equal the total file size.
        /// </summary>
        [Fact]
        public async Task Tus_Patch_TwoChunks_BothSucceedAndOffsetAdvances()
        {
            // Use enough bytes to split into two chunks; actual chunk-size is tusdotnet-configured.
            var fileBytes = new byte[22]; // same length as MinJpegBytes × 1 for simplicity
            Array.Copy(MinJpegBytes, fileBytes, MinJpegBytes.Length);

            var half = fileBytes.Length / 2;
            var chunk1 = fileBytes[..half];
            var chunk2 = fileBytes[half..];

            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: fileBytes, itemId: 1);

            var r1 = await PatchTusChunkAsync(client, location, offset: 0, data: chunk1);
            r1.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var r2 = await PatchTusChunkAsync(client, location, offset: half, data: chunk2);
            r2.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            r2.Headers.TryGetValues("Upload-Offset", out var offsets).ShouldBeTrue();
            offsets.ShouldContain(fileBytes.Length.ToString());
        }

        /// <summary>
        /// After a partial PATCH, HEAD must return the current byte offset so tus-js-client
        /// can resume from exactly where it stopped (network failure simulation).
        /// </summary>
        [Fact]
        public async Task Tus_Head_AfterPartialUpload_ReturnsCorrectResumeOffset()
        {
            var fileBytes = new byte[22];
            Array.Copy(MinJpegBytes, fileBytes, MinJpegBytes.Length);
            var half = fileBytes.Length / 2;

            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: fileBytes, itemId: 1);

            // Upload only first half
            await PatchTusChunkAsync(client, location, offset: 0, data: fileBytes[..half]);

            // HEAD should report current offset = half
            var headRequest = new HttpRequestMessage(HttpMethod.Head, location);
            headRequest.Headers.Add(TusResumableHeader, TusResumableVersion);
            var headResponse = await client.SendAsync(headRequest, CancellationToken);

            headResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            headResponse.Headers.TryGetValues("Upload-Offset", out var offsets).ShouldBeTrue();
            offsets.ShouldContain(half.ToString());
        }

        // ─── CompleteUpload endpoint ─────────────────────────────────────────────────

        /// <summary>
        /// After a full TUS upload, POST to /Catalog/Image/CompleteUpload with the TUS upload URL
        /// must trigger server-side image assembly and persistence → 200 OK.
        ///
        /// This is the bridge between the TUS infrastructure and the existing IImageService.
        /// The request body carries { tusUploadUrl, itemId }.
        /// </summary>
        [Fact]
        public async Task CompleteUpload_AfterFullTusUpload_Returns200()
        {
            const int itemId = 1;
            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: MinJpegBytes, itemId: itemId);

            // Upload the full file
            var patchResp = await PatchTusChunkAsync(client, location, offset: 0, data: MinJpegBytes);
            patchResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // CompleteUpload uses [IgnoreAntiforgeryToken] — no CSRF header needed.
            var payload = JsonSerializer.Serialize(new { tusUploadUrl = location.ToString(), itemId });
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "/Catalog/Image/CompleteUpload")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(requestMsg, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        /// <summary>
        /// CompleteUpload before the TUS upload is finished (not all bytes uploaded) → 422 Unprocessable.
        /// The server must not accept incomplete uploads into the image store.
        /// </summary>
        [Fact]
        public async Task CompleteUpload_BeforeUploadComplete_Returns422()
        {
            const int itemId = 1;
            var fileBytes = new byte[22];
            Array.Copy(MinJpegBytes, fileBytes, MinJpegBytes.Length);
            var half = fileBytes.Length / 2;

            var client = await CreateAuthenticatedClientAsync();
            var location = await CreateTusUploadAsync(client, fileBytes: fileBytes, itemId: itemId);

            // Upload only half
            await PatchTusChunkAsync(client, location, offset: 0, data: fileBytes[..half]);

            // CompleteUpload uses [IgnoreAntiforgeryToken] — no CSRF header needed.
            var payload = JsonSerializer.Serialize(new { tusUploadUrl = location.ToString(), itemId });
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, "/Catalog/Image/CompleteUpload")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(requestMsg, CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        }

        /// <summary>
        /// Anonymous POST to CompleteUpload → the cookie auth handler challenges with a 302
        /// redirect to the login page (this is standard MVC/cookie-auth behaviour — not 401).
        /// The test uses a no-redirect client so we observe the raw 302 before it is followed.
        /// </summary>
        [Fact]
        public async Task CompleteUpload_AnonymousRequest_RedirectsToLogin()
        {
            // Do NOT follow redirects — we want to observe the raw auth challenge.
            var client = (_factory as CustomWebApplicationFactory<Startup>)!
                .CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });

            var payload = JsonSerializer.Serialize(new { tusUploadUrl = "/tus/fake-id", itemId = 1 });
            var response = await client.PostAsync(
                "/Catalog/Image/CompleteUpload",
                new StringContent(payload, Encoding.UTF8, "application/json"),
                CancellationToken);

            // Cookie auth returns 302 Found → redirects to /Identity/Account/Login
            response.StatusCode.ShouldBe(HttpStatusCode.Found);
            response.Headers.Location?.ToString().ShouldContain("Login");
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends the TUS creation POST and returns the Location URI of the new upload resource.
        /// Encodes filename and itemId in Upload-Metadata as required by tusdotnet.
        /// </summary>
        private async Task<Uri> CreateTusUploadAsync(HttpClient client, byte[] fileBytes, int itemId)
        {
            var filenameMeta = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-image.jpg"));
            var itemIdMeta = Convert.ToBase64String(Encoding.UTF8.GetBytes(itemId.ToString()));

            var request = new HttpRequestMessage(HttpMethod.Post, TusEndpoint);
            request.Headers.Add(TusResumableHeader, TusResumableVersion);
            request.Headers.Add("Upload-Length", fileBytes.Length.ToString());
            request.Headers.Add("Upload-Metadata", $"filename {filenameMeta},itemId {itemIdMeta}");
            request.Content = new ByteArrayContent(Array.Empty<byte>());
            request.Content.Headers.ContentLength = 0;

            var response = await client.SendAsync(request, CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Created,
                $"TUS creation POST failed with {(int)response.StatusCode}");

            return response.Headers.Location!;
        }

        /// <summary>
        /// Sends a TUS PATCH request for the given upload resource, uploading <paramref name="data"/>
        /// starting at <paramref name="offset"/>.
        /// </summary>
        private async Task<HttpResponseMessage> PatchTusChunkAsync(
            HttpClient client, Uri location, long offset, byte[] data)
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, location);
            request.Headers.Add(TusResumableHeader, TusResumableVersion);
            request.Headers.Add("Upload-Offset", offset.ToString());
            request.Content = new ByteArrayContent(data);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");

            return await client.SendAsync(request, CancellationToken);
        }
    }
}
