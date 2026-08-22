using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CopyGIF.Converters
{
    public sealed class PreviewImageConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            string source = value as string;

            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            try
            {
                if (File.Exists(source))
                {
                    using (var stream = new FileStream(
                        source,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    {
                        var localImage = new BitmapImage();
                        localImage.BeginInit();
                        localImage.CacheOption = BitmapCacheOption.OnLoad;
                        localImage.DecodePixelWidth = 480;
                        localImage.StreamSource = stream;
                        localImage.EndInit();
                        localImage.Freeze();

                        return localImage;
                    }
                }

                if (!Uri.TryCreate(
                        source,
                        UriKind.Absolute,
                        out Uri remoteUri) ||
                    remoteUri.Scheme != Uri.UriSchemeHttps)
                {
                    return null;
                }

                var remoteImage = new BitmapImage();
                remoteImage.BeginInit();
                remoteImage.CacheOption = BitmapCacheOption.OnDemand;
                remoteImage.CreateOptions =
                    BitmapCreateOptions.IgnoreColorProfile;
                remoteImage.DecodePixelWidth = 480;
                remoteImage.UriSource = remoteUri;
                remoteImage.EndInit();

                return remoteImage;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
