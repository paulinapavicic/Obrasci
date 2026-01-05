namespace Obrasci.Services.ImageProcessing
{
    public class OriginalStrategy : IImageProcessingStrategy
    {
        public string Name => "Original";

        public async Task ProcessAsync(Stream input, Stream output, string contentType)
        {
            await input.CopyToAsync(output);
        }
    }
}
