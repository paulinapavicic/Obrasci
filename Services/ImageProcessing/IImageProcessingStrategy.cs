namespace Obrasci.Services.ImageProcessing
{
    public interface IImageProcessingStrategy
    {
        Task ProcessAsync(Stream input, Stream output, string contentType);
        string Name { get; }
    }
}
