using FluentAssertions;
using Obrasci.Services.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Tests.Unit
{
    public class ImageProcessingStrategyTests
    {
        private static MemoryStream MakeJpeg(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height);

            var stream = new MemoryStream();

            image.SaveAsJpeg(stream);
            stream.Position = 0;

            return stream;
        }

        private static MemoryStream MakeColorfulPng(
            int width = 2,
            int height = 2)
        {
            using var image = new Image<Rgba32>(width, height);

            image[0, 0] = new Rgba32(255, 0, 0);
            image[1, 0] = new Rgba32(0, 255, 0);
            image[0, 1] = new Rgba32(0, 0, 255);
            image[1, 1] = new Rgba32(255, 255, 0);

            var stream = new MemoryStream();

            image.SaveAsPng(stream);
            stream.Position = 0;

            return stream;
        }

        [Fact]
        public void OriginalStrategy_has_expected_name()
        {
            new OriginalStrategy().Name.Should().Be("Original");
        }

        [Fact]
        public void ResizeStrategy_has_expected_name()
        {
            new ResizeStrategy().Name.Should().Be("Resize800");
        }

        [Fact]
        public void GrayscaleStrategy_has_expected_name()
        {
            new GrayscaleStrategy().Name.Should().Be("Grayscale");
        }

        [Fact]
        public async Task ResizeStrategy_outputs_image_with_width_800()
        {
            using var src = MakeJpeg(1600, 1200);
            using var dst = new MemoryStream();

            await new ResizeStrategy().ProcessAsync(
                src,
                dst,
                "image/jpeg");

            dst.Position = 0;

            using var result = await Image.LoadAsync(dst);

            result.Width.Should().Be(800);
        }

        [Fact]
        public async Task ResizeStrategy_resizes_landscape_image_and_preserves_aspect_ratio()
        {
            using var src = MakeJpeg(1600, 1200);
            using var dst = new MemoryStream();

            await new ResizeStrategy().ProcessAsync(
                src,
                dst,
                "image/jpeg");

            dst.Position = 0;

            using var result = await Image.LoadAsync(dst);

            result.Width.Should().Be(800);
            result.Height.Should().Be(600);
        }

        [Fact]
        public async Task ResizeStrategy_outputs_valid_image_for_portrait_input()
        {
            using var src = MakeJpeg(800, 1600);
            using var dst = new MemoryStream();

            await new ResizeStrategy().ProcessAsync(
                src,
                dst,
                "image/jpeg");

            dst.Length.Should().BeGreaterThan(0);

            dst.Position = 0;

            using var result = await Image.LoadAsync(dst);

            result.Width.Should().BeGreaterThan(0);
            result.Height.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task OriginalStrategy_passes_bytes_through_unchanged()
        {
            using var src = MakeJpeg(40, 40);
            var originalBytes = src.ToArray();

            using var dst = new MemoryStream();

            await new OriginalStrategy().ProcessAsync(
                src,
                dst,
                "image/jpeg");

            dst.ToArray().Should().Equal(originalBytes);
        }

        [Fact]
        public async Task OriginalStrategy_outputs_a_decodable_image()
        {
            using var src = MakeJpeg(80, 60);
            using var dst = new MemoryStream();

            await new OriginalStrategy().ProcessAsync(
                src,
                dst,
                "image/jpeg");

            dst.Position = 0;

            using var result = await Image.LoadAsync(dst);

            result.Width.Should().Be(80);
            result.Height.Should().Be(60);
        }

        [Fact]
        public async Task GrayscaleStrategy_converts_colored_pixels_to_grayscale()
        {
            using var src = MakeColorfulPng();
            using var dst = new MemoryStream();

            await new GrayscaleStrategy().ProcessAsync(
                src,
                dst,
                "image/png");

            dst.Position = 0;

            using var result = await Image.LoadAsync<Rgba32>(dst);

            var pixel = result[0, 0];

            Math.Abs((int)pixel.R - pixel.G).Should().BeLessThanOrEqualTo(3);
            Math.Abs((int)pixel.G - pixel.B).Should().BeLessThanOrEqualTo(3);
        }

        [Fact]
        public async Task GrayscaleStrategy_preserves_image_dimensions()
        {
            using var src = MakeColorfulPng(width: 30, height: 20);
            using var dst = new MemoryStream();

            await new GrayscaleStrategy().ProcessAsync(
                src,
                dst,
                "image/png");

            dst.Position = 0;

            using var result = await Image.LoadAsync(dst);

            result.Width.Should().Be(30);
            result.Height.Should().Be(20);
        }

        [Fact]
        public async Task GrayscaleStrategy_writes_jpeg_output_even_when_input_is_png()
        {
            using var src = MakeColorfulPng();
            using var dst = new MemoryStream();

            await new GrayscaleStrategy().ProcessAsync(
                src,
                dst,
                "image/png");

            dst.Position = 0;

            var format = await Image.DetectFormatAsync(dst);

            format.Should().NotBeNull();
            format!.Name.Should().Be("JPEG");
        }

        [Fact]
        public async Task GrayscaleStrategy_with_invalid_image_bytes_throws()
        {
            await using var src = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(
                    "this is not an image"));

            await using var dst = new MemoryStream();

            Func<Task> act = () =>
                new GrayscaleStrategy().ProcessAsync(
                    src,
                    dst,
                    "image/jpeg");

            await act.Should().ThrowAsync<Exception>();
        }

        [Fact]
        public async Task OriginalStrategy_copies_all_bytes_unchanged()
        {
            var originalBytes = new byte[] { 10, 20, 30, 40, 50 };

            await using var input = new MemoryStream(originalBytes);
            await using var output = new MemoryStream();

            var strategy = new OriginalStrategy();

            await strategy.ProcessAsync(
                input,
                output,
                "application/octet-stream");

            output.ToArray().Should().Equal(originalBytes);
        }

        [Fact]
        public async Task OriginalStrategy_copies_only_remaining_bytes_from_current_input_position()
        {
            var originalBytes = new byte[] { 10, 20, 30, 40, 50 };

            await using var input = new MemoryStream(originalBytes);
            await using var output = new MemoryStream();

            input.Position = 2;

            var strategy = new OriginalStrategy();

            await strategy.ProcessAsync(
                input,
                output,
                "application/octet-stream");

            output.ToArray().Should().Equal(30, 40, 50);
        }

        [Fact]
        public async Task OriginalStrategy_with_empty_input_writes_empty_output()
        {
            await using var input = new MemoryStream();
            await using var output = new MemoryStream();

            var strategy = new OriginalStrategy();

            await strategy.ProcessAsync(
                input,
                output,
                "application/octet-stream");

            output.Length.Should().Be(0);
        }

        [Fact]
        public async Task OriginalStrategy_appends_to_existing_output_at_its_current_position()
        {
            await using var input = new MemoryStream(
                new byte[] { 30, 40 });

            await using var output = new MemoryStream();

            await output.WriteAsync(new byte[] { 10, 20 });

            var strategy = new OriginalStrategy();

            await strategy.ProcessAsync(
                input,
                output,
                "application/octet-stream");

            output.ToArray().Should().Equal(10, 20, 30, 40);
        }
    }
}