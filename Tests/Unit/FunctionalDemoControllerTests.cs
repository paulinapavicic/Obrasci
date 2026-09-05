using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Obrasci.Controllers;
using Obrasci.ViewModels;
using Xunit;

namespace Tests.Unit
{
    public class FunctionalDemoControllerTests
    {
        [Fact]
        public void Index_returns_view_with_expected_functional_demo_model()
        {
         
            var controller = new FunctionalDemoController();

           
            var result = controller.Index();

            
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeOfType<FunctionalDemoViewModel>()
                .Subject;

            model.SamplePhotos.Should().HaveCount(4);

            model.SamplePhotos.Select(photo => photo.FileName)
                .Should()
                .Equal(
                    "beach.jpg",
                    "mountain.png",
                    "city-night.jpg",
                    "forest.jpg");

            model.FilterDescription.Should().Be(
                "Size >= 1,500,000 bytes AND hashtag contains 'nature'");

            model.FilteredPhotos.Should().ContainSingle();

            model.FilteredPhotos[0].FileName
                .Should()
                .Be("mountain.png");

            model.FilteredPhotos[0].SizeBytes
                .Should()
                .BeGreaterThanOrEqualTo(1_500_000);

            model.FilteredPhotos[0].Hashtags
                .Should()
                .Contain("nature");

            model.FileNames.Should().Equal(
                "beach.jpg",
                "mountain.png",
                "city-night.jpg",
                "forest.jpg");

            model.TotalBytes.Should().Be(
                1_200_000L +
                2_200_000L +
                1_800_000L +
                950_000L);

            model.ParsedHashtags.Should().Equal(
                "summer",
                "beach",
                "sea",
                "travel");

            model.CountByAuthor["anela"].Should().Be(2);
            model.CountByAuthor["marko"].Should().Be(2);
        }
    }
}