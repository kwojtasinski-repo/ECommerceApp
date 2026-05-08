using ECommerceApp.Application.Catalog.Images.Models;
using ECommerceApp.Application.Catalog.Images.Upload;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.POCO;
using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Shared.TestInfrastructure;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class ImageControllerTests : ApiTestBase<ImageApiTestFactory>, IClassFixture<ImageApiTestFactory>
    {
        private readonly DirectoryInfo _testData;

        public ImageControllerTests(ImageApiTestFactory factory, ITestOutputHelper output)
            : base(factory, output)
        {
            _testData = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.GetDirectories().Where(d => d.Name == "TestData").FirstOrDefault();
        }

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            await _factory.EnsureSeedCatalogData(CancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            var imageRepository = _factory.Services.GetService(typeof(IImageRepository)) as IImageRepository;
            var fileStore = _factory.Services.GetService(typeof(IFileStore)) as IFileStore;
            var images = await imageRepository.GetAllImages();

            foreach (var image in images)
            {
                fileStore.DeleteFile(image.FileName.Value);
            }

            GC.SuppressFinalize(this);
            await base.DisposeAsync();
        }

        private async Task<FlurlClient> GetLoggingClient()
        {
            var client = await _factory.GetAuthenticatedClient(CancellationToken);
            client.Settings.OnErrorAsync = async call =>
            {
                var body = call.Response?.ResponseMessage?.Content != null
                    ? await call.Response.ResponseMessage.Content.ReadAsStringAsync()
                    : string.Empty;
                _output.WriteLine($"[HTTP {call.Response?.StatusCode}] {call.Request.Url}: {body}");
            };
            return client;
        }

        [Fact]
        public async Task given_valid_id_should_return_imageAsync()        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().First().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var addPoco = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(addPoco);
            var id = await client.Request("api/images")
                .PostAsync(multiContent, CancellationToken)
                .ReceiveJson<int>();

            var response = await client.Request($"api/images/{id}")
                .AllowAnyHttpStatus()
                .GetAsync(CancellationToken);
            var bytes = await response.ResponseMessage.Content.ReadAsByteArrayAsync(CancellationToken);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            bytes.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task given_valid_image_should_add()
        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "apple-iphone-13.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);

            var id = await client.Request($"api/images")
                .PostAsync(multiContent, cancellationToken: CancellationToken)
                .ReceiveJson<int>();

            id.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_valid_images_should_add()
        {
            var client = await GetLoggingClient();
            var filePaths = _testData.GetFiles().ToList();
            var formFiles = new List<IFormFile>();
            foreach (var filePath in filePaths)
            {
                var file = await Utilities.CreateIFormFileFrom(filePath.FullName);
                formFiles.Add(file);
            }
            var images = new AddImagesPOCO { ItemId = _factory.SeededItemId, Files = formFiles };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagesPOCO>(images);

            var ids = await client.Request($"api/images/multi-upload")
                .PostAsync(multiContent, cancellationToken: CancellationToken)
                .ReceiveJson<List<int>>();

            ids.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task given_id_image_should_delete()
        {
            var client = await GetLoggingClient();
            var filePath = _testData.GetFiles().Where(f => f.Name == "redmi-note-10.jpg").FirstOrDefault().FullName;
            var file = await Utilities.CreateIFormFileFrom(filePath);
            var image = new AddImagePOCO { File = file, ItemId = _factory.SeededItemId };
            var multiContent = Utilities.SerializeObjectWithImageToBytes<AddImagePOCO>(image);
            var id = await client.Request($"api/images")
                .AllowHttpStatus(HttpStatusCode.OK, HttpStatusCode.Created)
                .PostAsync(multiContent, cancellationToken: CancellationToken)
                .ReceiveJson<int>();

            var response = await client.Request($"api/images/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync(cancellationToken: CancellationToken);

            var afterDeleteResponse = await client.Request($"api/images/{id}")
                            .AllowAnyHttpStatus()
                            .GetAsync(cancellationToken: CancellationToken);
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            afterDeleteResponse.StatusCode.ShouldBe((int)HttpStatusCode.OK);
        }

        [Fact]
        public async Task init_upload_given_valid_request_should_return_session()
        {
            var client = await GetLoggingClient();
            var itemId = await _factory.CreateFreshItemAsync(CancellationToken);
            var filePath = _testData.GetFiles().First().FullName;
            var fileBytes = await File.ReadAllBytesAsync(filePath, CancellationToken);
            var request = new InitUploadRequest
            {
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = fileBytes.Length,
                ItemId = itemId
            };

            var response = await client.Request("api/images/init-upload")
                .AllowAnyHttpStatus()
                .WithHeader("Content-Type", "application/json")
                .PostStringAsync(JsonSerializer.Serialize(request), cancellationToken: CancellationToken);

            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            var body = await response.ResponseMessage.Content.ReadAsStringAsync(CancellationToken);
            var result = JsonSerializer.Deserialize<InitUploadResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            result.ShouldNotBeNull();
            result.SessionId.ShouldNotBe(Guid.Empty);
            result.ChunkSize.ShouldBeGreaterThan(0);
            result.TotalChunks.ShouldBeGreaterThan(0);
            result.ChunkIds.ShouldNotBeNull();
            result.ChunkIds.Length.ShouldBe(result.TotalChunks);
        }

        [Fact]
        public async Task upload_chunk_single_chunk_file_should_complete()
        {
            var client = await GetLoggingClient();
            var itemId = await _factory.CreateFreshItemAsync(CancellationToken);
            var filePath = _testData.GetFiles().Where(f => f.Name == "apple-iphone-13.jpg").FirstOrDefault().FullName;
            var fileBytes = await File.ReadAllBytesAsync(filePath, CancellationToken);
            var fileName = Path.GetFileName(filePath);

            // Init
            var initRequest = new InitUploadRequest { FileName = fileName, FileSizeBytes = fileBytes.Length, ItemId = itemId };
            var initResponse = await client.Request("api/images/init-upload")
                .WithHeader("Content-Type", "application/json")
                .PostStringAsync(JsonSerializer.Serialize(initRequest), cancellationToken: CancellationToken)
                .ReceiveString();
            var session = JsonSerializer.Deserialize<InitUploadResponse>(initResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Upload all chunks
            UploadChunkResponse lastResponse = null;
            foreach (var chunkId in session.ChunkIds)
            {
                var start = (chunkId - 1) * session.ChunkSize;
                var slice = fileBytes.Skip(start).Take(session.ChunkSize).ToArray();

                var multipartData = new MultipartFormDataContent
                {
                    { new StringContent(session.SessionId.ToString()), "sessionId" },
                    { new StringContent(chunkId.ToString()), "chunkId" },
                    { new ByteArrayContent(slice) { Headers = { { "Content-Disposition", $"form-data; name=\"chunk\"; filename=\"{fileName}\"" } } }, "chunk", fileName }
                };

                var chunkRaw = await client.Request("api/images/upload-chunk")
                    .AllowAnyHttpStatus()
                    .PostAsync(multipartData, cancellationToken: CancellationToken)
                    .ReceiveString();
                lastResponse = JsonSerializer.Deserialize<UploadChunkResponse>(chunkRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            lastResponse.ShouldNotBeNull();
            lastResponse.Complete.ShouldBeTrue();
            lastResponse.ReceivedCount.ShouldBe(session.TotalChunks);
        }

        [Fact]
        public async Task upload_chunk_multi_chunk_file_should_complete_and_progress_monotonically()
        {
            var client = await GetLoggingClient();
            var itemId = await _factory.CreateFreshItemAsync(CancellationToken);
            var filePath = _testData.GetFiles().Where(f => f.Name == "redmi-note-10.jpg").FirstOrDefault().FullName;
            var fileBytes = await File.ReadAllBytesAsync(filePath, CancellationToken);
            var fileName = Path.GetFileName(filePath);

            var initRequest = new InitUploadRequest { FileName = fileName, FileSizeBytes = fileBytes.Length, ItemId = itemId };
            var initRaw = await client.Request("api/images/init-upload")
                .WithHeader("Content-Type", "application/json")
                .PostStringAsync(JsonSerializer.Serialize(initRequest), cancellationToken: CancellationToken)
                .ReceiveString();
            var session = JsonSerializer.Deserialize<InitUploadResponse>(initRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var receivedCounts = new List<int>();
            foreach (var chunkId in session.ChunkIds)
            {
                var start = (chunkId - 1) * session.ChunkSize;
                var slice = fileBytes.Skip(start).Take(session.ChunkSize).ToArray();

                var fd = new MultipartFormDataContent();
                fd.Add(new StringContent(session.SessionId.ToString()), "sessionId");
                fd.Add(new StringContent(chunkId.ToString()), "chunkId");
                fd.Add(new ByteArrayContent(slice) { Headers = { { "Content-Disposition", $"form-data; name=\"chunk\"; filename=\"{fileName}\"" } } }, "chunk", fileName);

                var chunkRaw = await client.Request("api/images/upload-chunk")
                    .AllowAnyHttpStatus()
                    .PostAsync(fd, cancellationToken: CancellationToken)
                    .ReceiveString();
                var res = JsonSerializer.Deserialize<UploadChunkResponse>(chunkRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                receivedCounts.Add(res.ReceivedCount);
            }

            // receivedCount must be strictly monotonically increasing
            for (var i = 1; i < receivedCounts.Count; i++)
                receivedCounts[i].ShouldBeGreaterThan(receivedCounts[i - 1]);

            receivedCounts.Last().ShouldBe(session.TotalChunks);
        }
    }
}

