using ECommerceApp.Application.POCO;
using ECommerceApp.Application.Services.Items;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class ImageServiceTests : BaseTest<IImageService>
    {
        [Fact]
        public void given_valid_item_id_should_return_images()
        {
            var id = 1;

            var images = _service.GetImagesByItemId(id);

            images.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_images_in_db_should_return_list()
        {
            var images = _service.GetAll();

            images.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_search_string_should_return_list_images()
        {
            var searchString = "";

            var images = _service.GetAll(searchString);

            images.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_invalid_search_string_should_return_empty_list_images()
        {
            var searchString = "absdgery34575468";

            var images = _service.GetAll(searchString);

            images.Count.ShouldBe(0);
        }
    }
}
