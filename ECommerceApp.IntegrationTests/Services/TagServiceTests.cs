using ECommerceApp.Application.Services.Items;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace ECommerceApp.IntegrationTests.Services
{
    public class TagServiceTests : BaseTest<ITagService>
    {
        [Fact]
        public void given_valid_should_return_tag_details()
        {
            var id = 1;

            var tag = _service.GetTagDetails(id);

            tag.ShouldNotBeNull();
            tag.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_should_return_null_tag_details()
        {
            var id = 134646;

            var tag = _service.GetTagDetails(id);

            tag.ShouldBeNull();
        }

        [Fact]
        public void given_valid_should_return_tag()
        {
            var id = 1;

            var tag = _service.GetTagById(id);

            tag.ShouldNotBeNull();
            tag.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_should_return_null_tag()
        {
            var id = 18554;

            var tag = _service.GetTagById(id);

            tag.ShouldBeNull();
        }

        [Fact]
        public void given_valid_expression_should_return_tags()
        {
            var tags = _service.GetTags(t => true);

            tags.Count().ShouldBeGreaterThan(0);
        }

        [Fact]
        public void given_valid_page_size_page_number_search_string_should_return_tags()
        {
            var pageSize = 20;
            var pageNo = 1;
            var searchString = "";

            var tags = _service.GetTags(pageSize, pageNo, searchString);

            tags.Count.ShouldBeGreaterThan(0);
            tags.Tags.Count.ShouldBeGreaterThan(0);
            tags.SearchString.ShouldBe(searchString);
            tags.CurrentPage.ShouldBe(pageNo);
            tags.PageSize.ShouldBe(pageSize);
        }

        [Fact]
        public void given_valid_id_should_delete_tag()
        {
            var tag = new TagVm { Id = 0, Name = "NameTag2" };
            var id = _service.AddTag(tag);

            _service.DeleteTag(id);

            var tagDeleted = _service.Get(id);
            tagDeleted.ShouldBeNull();
        }
    }
}
