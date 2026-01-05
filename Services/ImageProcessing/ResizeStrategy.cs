using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Obrasci.Services.ImageProcessing
{
    public class ResizeStrategy : IImageProcessingStrategy
    {
        public string Name => "Resize800";

        public async Task ProcessAsync(Stream input, Stream output, string contentType)
        {
            using var image = await Image.LoadAsync(input);
            image.Mutate(x => x.Resize(800, 0)); // width=800
            await image.SaveAsJpegAsync(output); 
        }
    }
}
