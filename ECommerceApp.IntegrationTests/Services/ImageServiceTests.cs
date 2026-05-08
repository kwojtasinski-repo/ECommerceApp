using ECommerceApp.Application.Catalog.Images.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ImageServiceTests : BcBaseTest<IImageService>
    {
        public ImageServiceTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task given_valid_item_id_should_return_images()
        {
            var id = 1;

            var images = await _service.GetImagesByItemId(id);

            images.ShouldNotBeNull();
        }

        [Fact]
        public async Task given_images_in_db_should_return_list()
        {
            var images = await _service.GetAll();

            images.ShouldNotBeNull();
        }

        [Fact]
        public async Task given_valid_search_string_should_return_list_images()
        {
            var searchString = "";

            var images = await _service.GetAll(searchString);

            images.ShouldNotBeNull();
        }

        [Fact]
        public async Task given_invalid_search_string_should_return_empty_list_images()
        {
            var searchString = "absdgery34575468";

            var images = await _service.GetAll(searchString);

            images.Count.ShouldBe(0);
        }
    }
}

