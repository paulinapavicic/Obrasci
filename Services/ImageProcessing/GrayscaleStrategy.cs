using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Obrasci.Services.ImageProcessing
{
    public class GrayscaleStrategy : IImageProcessingStrategy
    {
        public string Name => "Grayscale";

        public async Task ProcessAsync(Stream input, Stream output, string contentType)
        {
            using var image = await Image.LoadAsync(input);
            image.Mutate(x => x.Grayscale());
            await image.SaveAsJpegAsync(output);
        }
    }
}
