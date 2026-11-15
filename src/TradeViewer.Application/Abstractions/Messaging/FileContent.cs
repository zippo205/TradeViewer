namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>A file as the value of a handler; the endpoint streams it to the browser and disposes the content.</summary>
public sealed class FileContent
{
    public FileContent(Stream content, string fileName, string mediaType)
    {
        Content = content;
        FileName = fileName;
        MediaType = mediaType;
    }

    public Stream Content { get; }

    public string FileName { get; }

    public string MediaType { get; }
}
