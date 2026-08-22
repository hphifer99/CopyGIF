using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace CopyGIF.Models
{
    [DataContract]
    public sealed class GifItem : INotifyPropertyChanged
    {
        private bool _isFavorite;
        private string _localFilePath;
        private string _localPreviewFilePath;
        private bool _isAnimatedPreviewEnabled;

        [DataMember(Name = "Id")]
        public long Id { get; set; }

        [DataMember(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [DataMember(Name = "ThumbnailUrl")]
        public string ThumbnailUrl { get; set; } = string.Empty;

        [DataMember(Name = "FullGifUrl")]
        public string FullGifUrl { get; set; } = string.Empty;

        [DataMember(Name = "PreviewGifUrl", EmitDefaultValue = false)]
        public string PreviewGifUrl { get; set; } = string.Empty;

        [DataMember(Name = "Width")]
        public int Width { get; set; }

        [DataMember(Name = "Height")]
        public int Height { get; set; }

        [DataMember(Name = "LocalFilePath", EmitDefaultValue = false)]
        public string LocalFilePath
        {
            get => _localFilePath;
            set
            {
                if (string.Equals(
                        _localFilePath,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _localFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayImageSource));
                OnPropertyChanged(nameof(AnimatedPreviewUri));
            }
        }

        [IgnoreDataMember]
        public string LocalPreviewFilePath
        {
            get => _localPreviewFilePath;
            set
            {
                if (string.Equals(
                        _localPreviewFilePath,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _localPreviewFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AnimatedPreviewUri));
            }
        }

        [DataMember(Name = "AddedUtc")]
        public DateTime AddedUtc { get; set; }

        [IgnoreDataMember]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                {
                    return;
                }

                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        [IgnoreDataMember]
        public string DisplayImageSource
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LocalFilePath) &&
                    File.Exists(LocalFilePath))
                {
                    return LocalFilePath;
                }

                return ThumbnailUrl;
            }
        }

        [IgnoreDataMember]
        public Uri AnimatedPreviewUri
        {
            get
            {
                if (!_isAnimatedPreviewEnabled)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(
                        LocalPreviewFilePath) &&
                    File.Exists(LocalPreviewFilePath))
                {
                    return new Uri(
                        Path.GetFullPath(LocalPreviewFilePath),
                        UriKind.Absolute);
                }

                if (!string.IsNullOrWhiteSpace(LocalFilePath) &&
                    File.Exists(LocalFilePath))
                {
                    return new Uri(
                        Path.GetFullPath(LocalFilePath),
                        UriKind.Absolute);
                }

                return null;
            }
        }

        public void SetAnimatedPreviewEnabled(bool isEnabled)
        {
            if (_isAnimatedPreviewEnabled == isEnabled)
            {
                return;
            }

            _isAnimatedPreviewEnabled = isEnabled;
            OnPropertyChanged(nameof(AnimatedPreviewUri));
        }

        public GifItem Clone()
        {
            return new GifItem
            {
                Id = Id,
                Title = Title,
                ThumbnailUrl = ThumbnailUrl,
                FullGifUrl = FullGifUrl,
                PreviewGifUrl = PreviewGifUrl,
                Width = Width,
                Height = Height,
                LocalFilePath = LocalFilePath,
                LocalPreviewFilePath = LocalPreviewFilePath,
                AddedUtc = AddedUtc,
                IsFavorite = IsFavorite,
                _isAnimatedPreviewEnabled =
                    _isAnimatedPreviewEnabled
            };
        }

        public bool HasSameIdentity(GifItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (Id != 0 && other.Id != 0)
            {
                return Id == other.Id;
            }

            return string.Equals(
                FullGifUrl,
                other.FullGifUrl,
                StringComparison.OrdinalIgnoreCase);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
