using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Web.E2E.Infrastructure;
using ECommerceApp.Web.E2E.PageObjects;
using Microsoft.Playwright;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.Web.E2E
{
    /// <summary>
    /// Guards the browser side of the Classic chunked upload (chunked-upload.js): a plain upload, and a
    /// resume after a mid-upload network failure that must re-send only the chunks the server lacks.
    /// </summary>
    public sealed class ChunkedImageUploadTests
    {
        private const int ChunkSizeBytes = 1_048_576;

        private readonly PlaywrightBrowserFixture _browserFixture;

        public ChunkedImageUploadTests(PlaywrightBrowserFixture browserFixture)
        {
            _browserFixture = browserFixture;
        }

        [Fact]
        public async Task EditProduct_UploadSmallImage_ShowsImageWithoutPageErrors()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productId = (await E2ESeed.BasketProductsAsync(services))[0];
            using var imageFile = TempImage.Create(BuildPng(paddingBytes: 0));
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();
            page.PageError += (_, error) => pageErrors.Add(error);
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            IProductEditPage editPage = await ProductEditPage.NavigateAsync(page, factory.ServerAddress, productId);
            await editPage.ChooseFileAsync(imageFile.Path);
            await editPage.ClickUploadAsync();
            await WaitForSavedImageCountAsync(services, productId, 1);
            await editPage.WaitForPageToSettleAsync();

            pageErrors.ShouldBeEmpty();
        }

        [Fact]
        public async Task EditProduct_UploadInterruptedMidway_ResumesSendingOnlyMissingChunks()
        {
            using var factory = new PlaywrightWebApplicationFactory();
            var services = factory.StartKestrelHost();
            await E2ESeed.AdminAsync(services);
            var productId = (await E2ESeed.BasketProductsAsync(services))[0];
            // ~2.5 MB => 3 chunks: 1 MiB, 1 MiB, ~0.4 MiB.
            using var imageFile = TempImage.Create(BuildPng(paddingBytes: 2_500_000));
            imageFile.Length.ShouldBeGreaterThan(2 * ChunkSizeBytes);
            await using var context = await _browserFixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();
            page.PageError += (_, error) => pageErrors.Add(error);
            await (await LoginPage.NavigateAsync(page, factory.ServerAddress)).LoginAsync(E2ESeed.AdminEmail, E2ESeed.Password);

            var sentChunkIds = new List<int>();
            var failChunkTwo = true;
            await page.RouteAsync("**/Catalog/Image/UploadChunk", async route =>
            {
                var chunkId = ReadChunkId(route.Request);
                lock (sentChunkIds)
                {
                    sentChunkIds.Add(chunkId);
                }

                if (failChunkTwo && chunkId == 2)
                {
                    await route.AbortAsync();
                    return;
                }

                await route.ContinueAsync();
            });

            IProductEditPage editPage = await ProductEditPage.NavigateAsync(page, factory.ServerAddress, productId);
            await editPage.ChooseFileAsync(imageFile.Path);
            await editPage.ClickUploadAsync();
            var error = await editPage.WaitForUploadErrorAsync();

            error.ShouldContain("fragmentu 2");
            error.ShouldContain("wznowić");
            sentChunkIds.Count(id => id == 1).ShouldBe(1);
            sentChunkIds.Count(id => id == 2).ShouldBeGreaterThan(1, "a failed chunk is retried before giving up");
            await AssertSavedImageCountAsync(services, productId, 0);

            // The user comes back later: same file, fresh page load, network healthy again.
            failChunkTwo = false;
            lock (sentChunkIds)
            {
                sentChunkIds.Clear();
            }

            editPage = await ProductEditPage.NavigateAsync(page, factory.ServerAddress, productId);
            await editPage.ChooseFileAsync(imageFile.Path);
            await editPage.ClickUploadAsync();
            var notice = await editPage.WaitForResumeNoticeAsync();
            await WaitForSavedImageCountAsync(services, productId, 1);
            await editPage.WaitForPageToSettleAsync();

            notice.ShouldContain("Wznowiono");
            sentChunkIds.ShouldBe(new[] { 2, 3 });
            await AssertSavedImageCountAsync(services, productId, 1);
            pageErrors.ShouldBeEmpty();
        }

        // The product details page caches the gallery for CatalogProductTtl, so a reloaded edit page cannot
        // be used to observe a fresh upload; the saved images are read from the database instead.
        private static async Task<int> SavedImageCountAsync(IServiceProvider services, int productId)
        {
            using var scope = services.CreateScope();
            var images = await scope.ServiceProvider.GetRequiredService<IImageService>().GetImagesByItemId(productId);
            return images.Count;
        }

        private static async Task AssertSavedImageCountAsync(IServiceProvider services, int productId, int expected)
        {
            (await SavedImageCountAsync(services, productId)).ShouldBe(expected);
        }

        private static async Task WaitForSavedImageCountAsync(IServiceProvider services, int productId, int expected)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (await SavedImageCountAsync(services, productId) != expected)
            {
                DateTime.UtcNow.ShouldBeLessThan(deadline, $"expected {expected} saved image(s) for product {productId}");
                await Task.Delay(100);
            }
        }

        private static int ReadChunkId(IRequest request)
        {
            var body = Encoding.Latin1.GetString(request.PostDataBuffer ?? Array.Empty<byte>());
            var match = Regex.Match(body, "name=\"chunkId\"\\r\\n\\r\\n(\\d+)");
            match.Success.ShouldBeTrue("UploadChunk request carried no chunkId field");
            return int.Parse(match.Groups[1].Value);
        }

        /// <summary>
        /// A decodable 1x1 PNG; <paramref name="paddingBytes"/> grows it through a legitimate tEXt chunk so
        /// the file stays a valid image (signature + IEND) while spanning several upload chunks.
        /// </summary>
        private static byte[] BuildPng(int paddingBytes)
        {
            using var output = new MemoryStream();
            output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            WritePngChunk(output, "IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0 });
            if (paddingBytes > 0)
            {
                var text = new byte[paddingBytes];
                Encoding.ASCII.GetBytes("Comment").CopyTo(text, 0);
                text[7] = 0;
                Array.Fill(text, (byte)'x', 8, paddingBytes - 8);
                WritePngChunk(output, "tEXt", text);
            }

            using var pixels = new MemoryStream();
            using (var zlib = new ZLibStream(pixels, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(new byte[] { 0, 0x1B, 0x6B, 0xC0 });
            }

            WritePngChunk(output, "IDAT", pixels.ToArray());
            WritePngChunk(output, "IEND", Array.Empty<byte>());
            return output.ToArray();
        }

        private static void WritePngChunk(Stream output, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            WriteBigEndian(output, (uint)data.Length);
            output.Write(typeBytes);
            output.Write(data);
            WriteBigEndian(output, Crc32(typeBytes, data));
        }

        private static void WriteBigEndian(Stream output, uint value)
        {
            output.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in type.Concat(data))
            {
                crc ^= b;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }

            return ~crc;
        }

        private sealed class TempImage : IDisposable
        {
            private TempImage(string path, long length)
            {
                Path = path;
                Length = length;
            }

            public string Path { get; }
            public long Length { get; }

            public static TempImage Create(byte[] content)
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"e2e-upload-{Guid.NewGuid():N}.png");
                File.WriteAllBytes(path, content);
                return new TempImage(path, content.Length);
            }

            public void Dispose()
            {
                File.Delete(Path);
            }
        }
    }
}
