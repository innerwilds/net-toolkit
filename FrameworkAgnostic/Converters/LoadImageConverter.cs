using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace FrameworkAgnostic.Converters;

/// <summary>
///     Loads an image from it's url and returns it's bytes
/// </summary>
public class LoadImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo cultureInfo)
    {
        Uri? uri;

        if (value is string url)
            Uri.TryCreate((string)value, UriKind.Absolute, out uri);
        else if (value is Uri alreadyUri)
            uri = alreadyUri;
        else
            return null;

        if (uri == null) return null;

        try
        {
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = client.Send(request);
            return ReadFully(response.Content.ReadAsStream());
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo cultureInfo)
    {
        throw new NotImplementedException();
    }

    public static byte[] ReadFully(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return ms.ToArray();
    }
}