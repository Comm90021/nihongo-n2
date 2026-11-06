using Avalonia.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Nihongo.Desktop.Services;

/// <summary>Vẽ trang PDF bằng bộ đọc PDF có sẵn của Windows. Chỉ nạp trang đang xem.</summary>
static class PdfPages
{
    static readonly Dictionary<string, PdfDocument> Docs = new();

    public static async Task<Bitmap> RenderAsync(string path, int pageNumber, int width)
    {
        if (!Docs.TryGetValue(path, out var doc))
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            Docs[path] = doc = await PdfDocument.LoadFromFileAsync(file);
        }
        var index = (uint)Math.Clamp(pageNumber - 1, 0, (int)doc.PageCount - 1);
        using var page = doc.GetPage(index);
        using var stream = new InMemoryRandomAccessStream();
        await page.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = (uint)Math.Max(400, width) });
        stream.Seek(0);
        using var memory = new MemoryStream();
        await stream.AsStreamForRead().CopyToAsync(memory);
        memory.Position = 0;
        return new Bitmap(memory);
    }

    public static async Task<int> PageCountAsync(string path)
    {
        await RenderAsync(path, 1, 400);
        return (int)Docs[path].PageCount;
    }
}
