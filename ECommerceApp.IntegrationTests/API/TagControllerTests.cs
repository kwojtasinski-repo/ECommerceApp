using ECommerceApp.API;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Newtonsoft.Json;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class TagControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public TagControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_tag()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/tags/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var tag = JsonConvert.DeserializeObject<TagDetailsVm>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            tag.ShouldNotBeNull();
            tag.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 13453;

            var response = await client.Request($"api/tags/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_tag_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TagVm { Id = 0, Name = "Tag2" };

            var response = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            id.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task given_invalid_tag_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TagVm { Id = 235 };

            var response = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_tag_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TagVm { Id = 0, Name = "Tag2" };
            var id = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag)
                .ReceiveJson<int>();
            tag.Id = id;
            var name = "Tag25";
            tag.Name = name;

            var response = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PutJsonAsync(tag);

            var tagUpdated = await client.Request($"api/tags/{id}")
                .AllowAnyHttpStatus()
                .GetJsonAsync<TagDetailsVm>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            tagUpdated.ShouldNotBeNull();
            tagUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_invalid_tag_when_update_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TagVm { Id = 234235 };

            var response = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PutJsonAsync(tag);

            response.StatusCode.ShouldBe((int) HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task given_valid_tag_should_delete()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TagVm { Id = 0, Name = "Tag2" };
            var id = await client.Request("api/tags")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag)
                .ReceiveJson<int>();

            var response = await client.Request($"api/tags/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var tagDeleted = await client.Request($"api/tags/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            tagDeleted.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_tags_in_db_should_return_tags()
        {
            var client = await _factory.GetAuthenticatedClient();

            var response = await client.Request($"api/tags")
                .AllowAnyHttpStatus()
                .GetAsync();

            var tags = JsonConvert.DeserializeObject<List<TagVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            tags.Count.ShouldBeGreaterThan(0);
        }
    }
}
