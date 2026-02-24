using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.Events;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Products
{
    public class ItemAggregateTests
    {
        [Fact]
        public void Create_ValidParameters_ShouldCreateDraftItem()
        {
            var (item, @event) = Item.Create("Test Product", 10.00m, 5, "Description", 1);

            item.Should().NotBeNull();
            item.Name.Value.Should().Be("Test Product");
            item.Cost.Amount.Should().Be(10.00m);
            item.Quantity.Value.Should().Be(5);
            item.Description.Value.Should().Be("Description");
            item.Status.Should().Be(ProductStatus.Draft);
            item.CategoryId.Value.Should().Be(1);
            @event.Should().NotBeNull();
            @event.Name.Should().Be("Test Product");
        }

        [Fact]
        public void Create_NegativeQuantity_ShouldThrowDomainException()
        {
            var act = () => Item.Create("Test", 10m, -1, "Desc", 1);

            act.Should().Throw<DomainException>().WithMessage("*negative*");
        }

        [Fact]
        public void Create_ZeroCost_ShouldThrowDomainException()
        {
            var act = () => Item.Create("Test", 0m, 1, "Desc", 1);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Create_InvalidCategoryId_ShouldThrowDomainException()
        {
            var act = () => Item.Create("Test", 10m, 1, "Desc", 0);

            act.Should().Throw<DomainException>().WithMessage("*CategoryId*positive*");
        }

        [Fact]
        public void Publish_DraftItem_ShouldReturnProductPublishedEvent()
        {
            var (item, _) = Item.Create("Test", 10m, 1, "Desc", 1);

            var @event = item.Publish();

            item.Status.Should().Be(ProductStatus.Published);
            @event.Should().NotBeNull();
            @event.Should().BeOfType<ProductPublished>();
        }

        [Fact]
        public void Publish_AlreadyPublishedItem_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 1, "Desc", 1);
            item.Publish();

            var act = () => item.Publish();

            act.Should().Throw<DomainException>().WithMessage("*already published*");
        }

        [Fact]
        public void Unpublish_PublishedItem_ShouldReturnProductUnpublishedEvent()
        {
            var (item, _) = Item.Create("Test", 10m, 1, "Desc", 1);
            item.Publish();

            var @event = item.Unpublish();

            item.Status.Should().Be(ProductStatus.Unpublished);
            @event.Should().NotBeNull();
            @event.Should().BeOfType<ProductUnpublished>();
        }

        [Fact]
        public void Unpublish_DraftItem_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 1, "Desc", 1);

            var act = () => item.Unpublish();

            act.Should().Throw<DomainException>().WithMessage("*Only published*");
        }

        [Fact]
        public void DecreaseQuantity_ValidAmount_ShouldDecrease()
        {
            var (item, _) = Item.Create("Test", 10m, 10, "Desc", 1);

            item.DecreaseQuantity(3);

            item.Quantity.Value.Should().Be(7);
        }

        [Fact]
        public void DecreaseQuantity_MoreThanAvailable_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 2, "Desc", 1);

            var act = () => item.DecreaseQuantity(5);

            act.Should().Throw<DomainException>().WithMessage("*Cannot decrease*");
        }

        [Fact]
        public void DecreaseQuantity_ZeroAmount_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 10, "Desc", 1);

            var act = () => item.DecreaseQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void IncreaseQuantity_ValidAmount_ShouldIncrease()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            item.IncreaseQuantity(3);

            item.Quantity.Value.Should().Be(8);
        }

        [Fact]
        public void IncreaseQuantity_ZeroAmount_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            var act = () => item.IncreaseQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void UpdateDetails_ValidParameters_ShouldUpdateAllFields()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            item.UpdateDetails("Updated", 20m, "New desc", 2);

            item.Name.Value.Should().Be("Updated");
            item.Cost.Amount.Should().Be(20m);
            item.Description.Value.Should().Be("New desc");
            item.CategoryId.Value.Should().Be(2);
        }

        [Fact]
        public void AddTag_NewTag_ShouldAddToCollection()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            item.AddTag(new TagId(1));

            item.ItemTags.Should().HaveCount(1);
            item.ItemTags[0].TagId.Value.Should().Be(1);
        }

        [Fact]
        public void AddTag_DuplicateTag_ShouldNotAddTwice()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            item.AddTag(new TagId(1));
            item.AddTag(new TagId(1));

            item.ItemTags.Should().HaveCount(1);
        }

        [Fact]
        public void RemoveTag_ExistingTag_ShouldRemoveFromCollection()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);
            item.AddTag(new TagId(1));
            item.AddTag(new TagId(2));

            item.RemoveTag(new TagId(1));

            item.ItemTags.Should().HaveCount(1);
            item.ItemTags[0].TagId.Value.Should().Be(2);
        }

        [Fact]
        public void AddImage_ValidImage_ShouldAddToCollection()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);

            item.AddImage("items/1/test.jpg", true, 0);

            item.Images.Should().HaveCount(1);
            item.Images[0].IsMain.Should().BeTrue();
        }

        [Fact]
        public void AddImage_MoreThanFive_ShouldThrowDomainException()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);
            for (int i = 0; i < 5; i++)
            {
                item.AddImage($"items/1/img{i}.jpg", i == 0, i);
            }

            var act = () => item.AddImage("items/1/extra.jpg", false, 5);

            act.Should().Throw<DomainException>().WithMessage("*at most 5*");
        }

        [Fact]
        public void ReplaceTags_NewTagSet_ShouldReplaceAll()
        {
            var (item, _) = Item.Create("Test", 10m, 5, "Desc", 1);
            item.AddTag(new TagId(1));
            item.AddTag(new TagId(2));

            item.ReplaceTags(new[] { new TagId(3), new TagId(4), new TagId(5) });

            item.ItemTags.Should().HaveCount(3);
        }

        [Fact]
        public void Publish_UnpublishedItem_ShouldRepublish()
        {
            var (item, _) = Item.Create("Test", 10m, 1, "Desc", 1);
            item.Publish();
            item.Unpublish();

            var @event = item.Publish();

            item.Status.Should().Be(ProductStatus.Published);
            @event.Should().NotBeNull();
        }
    }
}
