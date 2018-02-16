using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SteamFair
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectCapture : Page
    {
        public ConnectCapture()
        {
            this.InitializeComponent();
            imageElement.Source = new SoftwareBitmapSource();
            System.Diagnostics.Debug.WriteLine("Built, and waiting");
        }

        public async Task<string> Capture()
        {
            var allGroups = await MediaFrameSourceGroup.FindAllAsync();
            var eligibleGroups = allGroups.Select(g => new
            {
                Group = g,

                // For each source kind, find the source which offers that kind of media frame,
                // or null if there is no such source.
                SourceInfos = new MediaFrameSourceInfo[]
                {
        g.SourceInfos.FirstOrDefault(info => info.SourceKind == MediaFrameSourceKind.Color),
        g.SourceInfos.FirstOrDefault(info => info.SourceKind == MediaFrameSourceKind.Depth),
        g.SourceInfos.FirstOrDefault(info => info.SourceKind == MediaFrameSourceKind.Infrared),
                }
            }).Where(g => g.SourceInfos.Any(info => info != null)).ToList();

            if (eligibleGroups.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No source group with color, depth or infrared found.");
                return "No source group with color, depth or infrared found.";
            }

            var selectedGroupIndex = 0; // Select the first eligible group
            MediaFrameSourceGroup selectedGroup = eligibleGroups[selectedGroupIndex].Group;
            MediaFrameSourceInfo colorSourceInfo = eligibleGroups[selectedGroupIndex].SourceInfos[0];
            MediaFrameSourceInfo infraredSourceInfo = eligibleGroups[selectedGroupIndex].SourceInfos[1];
            MediaFrameSourceInfo depthSourceInfo = eligibleGroups[selectedGroupIndex].SourceInfos[2];

            var mediaCapture = new MediaCapture();

            var settings = new MediaCaptureInitializationSettings()
            {
                SourceGroup = selectedGroup,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };
            try
            {
                await mediaCapture.InitializeAsync(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MediaCapture initialization failed: " + ex.Message);
                return "MediaCapture initialization failed: " + ex.Message;
            }

            var colorFrameSource = mediaCapture.FrameSources[colorSourceInfo.Id];
            var preferredFormat = colorFrameSource.SupportedFormats[0];
            System.Diagnostics.Debug.WriteLine((int)preferredFormat.FrameRate.Numerator/preferredFormat.FrameRate.Denominator);
            if (preferredFormat == null)
            {
                // Our desired format is not supported
                return "Our desired format is not supported";
            }

            await colorFrameSource.SetFormatAsync(preferredFormat);

            MediaFrameReader mediaFrameReader;
            mediaFrameReader = await mediaCapture.CreateFrameReaderAsync(colorFrameSource, MediaEncodingSubtypes.Argb32);
            mediaFrameReader.FrameArrived += ColorFrameReader_FrameArrived;
            await mediaFrameReader.StartAsync();
            return "Success";
        }

        private SoftwareBitmap backBuffer;
        private bool taskRunning = false;
        private async void ColorFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            
            var mediaFrameReference = sender.TryAcquireLatestFrame();
            var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
            var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

            if (softwareBitmap != null)
            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                // Swap the processed frame to _backBuffer and dispose of the unused image.
                softwareBitmap = Interlocked.Exchange(ref backBuffer, softwareBitmap);
                softwareBitmap?.Dispose();

                // Changes to XAML ImageElement must happen on UI thread through Dispatcher
                var task = imageElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                // Don't let two copies of this task run at the same time.
                if (taskRunning)
                        {
                            return;
                        }
                        taskRunning = true;

                // Keep draining frames from the backbuffer until the backbuffer is empty.
                SoftwareBitmap latestBitmap;
                        while ((latestBitmap = Interlocked.Exchange(ref backBuffer, null)) != null)
                        {
                            var imageSource = (SoftwareBitmapSource)imageElement.Source;
                            await imageSource.SetBitmapAsync(latestBitmap);

                            latestBitmap.Dispose();
                            //await Task.Delay(TimeSpan.FromMilliseconds(6));
                        }

                        taskRunning = false;
                    });
            }

            mediaFrameReference.Dispose();
        }

        private async void imageCapture_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Clicked");
            System.Diagnostics.Debug.WriteLine(await Capture()); 
        }

        private async Task<SoftwareBitmap> Parse()
        {
            return new SoftwareBitmap(BitmapPixelFormat.Unknown,1080,720);
        }
    }
}
