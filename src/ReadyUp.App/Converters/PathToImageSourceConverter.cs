using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ReadyUp.App.Converters;

/// <summary>
/// Loads a local file path or a remote http(s) URL (SteamGridDB search
/// thumbnails) into a BitmapImage decoded at a fixed pixel width
/// (converter parameter, default 300) rather than full resolution — game
/// art can be several megapixels, and tiles only ever need a few hundred
/// pixels across. BitmapCacheOption.OnLoad releases the file handle
/// immediately, so a replaced local art file (Change Game Art) is picked
/// up on next binding refresh without a locked/stale image.
/// </summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var isRemote = Uri.TryCreate(path, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        if (!isRemote && !File.Exists(path))
        {
            return null;
        }

        var decodePixelWidth = parameter is string s && int.TryParse(s, out var parsed) ? parsed : 300;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.UriSource = isRemote ? uri! : new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
