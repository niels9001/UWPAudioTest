using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.Render;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AudioTest
{
    public sealed partial class MainPage : Page
    {
        private AudioGraph graph;
        private AudioFrameOutputNode frameOutputNode;
        private float currentPeak;


        public MainPage()
        {
            this.InitializeComponent();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            GetAudioDevices();
        }

        private async void GetAudioDevices()
        {
            DeviceInformationCollection inputDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector());

            ObservableCollection<DeviceInformation> Devices = new ObservableCollection<DeviceInformation>();

            foreach (DeviceInformation device in inputDevices)
            {
                Devices.Add(device);
            }

            DevicesBox.ItemsSource = inputDevices;
        }


        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            DeviceInformation SelectedDevice = DevicesBox.SelectedItem as DeviceInformation;
            AudioGraphSettings settings = new AudioGraphSettings(AudioRenderCategory.Media)
            {
                QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
            };

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);


            graph = result.Graph;

            // Create a device output node
            CreateAudioDeviceOutputNodeResult deviceOutputNodeResult = await graph.CreateDeviceOutputNodeAsync();
            AudioDeviceOutputNode deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;

            // Create a device input node using the default audio input device
            CreateAudioDeviceInputNodeResult deviceInputNodeResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Other, graph.EncodingProperties, SelectedDevice);

            if (deviceInputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Cannot create device input node
                System.Diagnostics.Debug.WriteLine(String.Format("Audio Device Input unavailable because {0}", deviceInputNodeResult.Status.ToString()));

                return; 
            }

            AudioDeviceInputNode deviceInputNode = deviceInputNodeResult.DeviceInputNode;

            frameOutputNode = graph.CreateFrameOutputNode();
            deviceInputNode.AddOutgoingConnection(frameOutputNode);

            AudioFrameInputNode frameInputNode = graph.CreateFrameInputNode();
            frameInputNode.AddOutgoingConnection(deviceOutputNode);

            // Attach to QuantumStarted event in order to receive synchronous updates from audio graph (to capture incoming audio).
            graph.QuantumStarted += GraphOnQuantumProcessed;

            graph.Start();
        }




        private void GraphOnQuantumProcessed(AudioGraph sender, object args)
        {
            var frame = frameOutputNode.GetFrame();
            ProcessFrameOutput(frame);
        }

        unsafe private void ProcessFrameOutput(AudioFrame frame)
        {
            using (AudioBuffer buffer = frame.LockBuffer(AudioBufferAccessMode.Read))
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                // get hold of the buffer pointer
                byte* dataInBytes;
                uint capacityInBytes;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes,
                    out capacityInBytes);

                var dataInFloat = (float*)dataInBytes;

                // examine
                float max = 0;
                for (int n = 0; n < graph.SamplesPerQuantum; n++)
                {
                    max = Math.Max(Math.Abs(dataInFloat[n]), max);
                }
                currentPeak = max;

                float x = currentPeak * 1000;

                double Bri = Math.Pow(x, 3); // Sensitivity slider value

                byte Brightness = (byte)Math.Round(Bri, 0); // Calculating to a 0 - 255 value to control the light brightness

                Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OutputText.Text = Brightness.ToString();
                });
            }
        }
    }


    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}