using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
#if WINDOWS_PHONE
using WASAPI.Net.WP8;
#elif WINDOWS_STOREAPP
using WASAPI.Net.Windows8;
using Windows.Devices.Enumeration;
#endif
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Buffer = System.Buffer;

namespace Microsoft.Xna.Framework.Audio
{
    public sealed class Microphone
    {

        public string Name { get; private set; }

        public static ReadOnlyCollection<Microphone> All { get { return GetAllMicrophones(); } }
        public TimeSpan BufferDuration { get; set; }
        public static Microphone Default { get { return GetDefaultMicrophone(); } }
        /// <summary>
        /// It's currently not possible to know if the microphone is a headset or not. Assume not. 
        /// </summary>
        public bool IsHeadset { get { return false; } }

        /// <summary>
        /// WASAPI is hardwired to 48kHz. 
        /// </summary>
        public int SampleRate { get { return 48000; } }
        public MicrophoneState State { get { return _state; } }

        public event EventHandler<EventArgs> BufferReady;

        private MicrophoneState _state = MicrophoneState.Stopped;
        private AudioCapture _audioCapture;
        private Queue<byte[]> _bufferQueue;
        private readonly string _deviceId; 

        internal Microphone(string name, string deviceId)
        {
            Name = name;
            _deviceId = deviceId;
        }

        public int GetData(byte[] buffer)
        {
            return GetData(buffer, 0, buffer.Length);
        }

        public int GetData(byte[] buffer, int offset, int count)
        {
            var data = _bufferQueue.Dequeue();
            if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentException("Buffer has not been initialized or is of zero length.");
            }
            if (buffer.Length < data.Length)
            {
                throw new ArgumentException("Buffer does not satisfy alignment requirements. Expected length was " + data.Length + " bytes.");
            }
            if (offset < 0 || offset >= buffer.Length || offset >= data.Length)
            {
                throw new ArgumentException("Offset is less than zero, out of bounds or does not satisfy alignment requirements.");
            }
            if (count < 0 || offset + count > buffer.Length || offset + count > buffer.Length)
            {
                throw new ArgumentException("Count is zero, the sum of count and offset exceeds the lenght of buffer or the sum of count and offset does not satisfy alignment requirements.");
            }
            var actualCount = count < data.Length ? count : data.Length;
            Buffer.BlockCopy(data, offset, buffer, offset, actualCount);
            return actualCount;
        }

        public TimeSpan GetSampleDuration(int sizeInBytes)
        {
            throw new NotImplementedException();
        }

        public int GetSampleSizeInBytes(TimeSpan duration)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            if (_state == MicrophoneState.Started)
            {
                return;
            }
            _bufferQueue = new Queue<byte[]>();
#if WINDOWS_PHONE
            _audioCapture = new AudioCapture();
#elif WINDOWS_STOREAPP
            _audioCapture = new AudioCapture(_deviceId);
#endif
            _audioCapture.BufferReady += AudioCaptureOnBufferReady;
            _audioCapture.Start();
            _state = MicrophoneState.Started;
        }

        public void Stop()
        {
            if (_state == MicrophoneState.Stopped)
            {
                return;
            }
            _audioCapture.Stop();
            _bufferQueue.Clear();
            _state = MicrophoneState.Stopped;
        }

        internal void OnBufferReady(EventArgs eventArgs)
        {
            var bufferReady = BufferReady;
            if (bufferReady != null)
            {
                bufferReady(this, eventArgs);
            }
        }

        private static Microphone GetDefaultMicrophone()
        {
#if WINDOWS_PHONE
            return new Microphone("Default microphone", string.Empty);
#elif WINDOWS_STOREAPP
            var deviceInformations = DeviceInformation.FindAllAsync(DeviceClass.AudioCapture).GetResults();
            foreach (var deviceInformation in deviceInformations.Where(deviceInformation => deviceInformation.IsDefault && deviceInformation.IsEnabled))
            {
                return new Microphone(deviceInformation.Name, deviceInformation.Id);
            }
            // according to msdn documentation, this function should return NULL if there are no microphones attached.
            return null;
#endif
            throw new NotImplementedException();
        }

        private static ReadOnlyCollection<Microphone> GetAllMicrophones()
        {
#if WINDOWS_PHONE
            return new ReadOnlyCollection<Microphone>(new List<Microphone>(){GetDefaultMicrophone()});
#elif WINDOWS_STOREAPP
            var deviceInformations = DeviceInformation.FindAllAsync(DeviceClass.AudioCapture).GetResults();
            var microphones = deviceInformations.Where(deviceInformation => deviceInformation.IsEnabled).Select(deviceInformation => new Microphone(deviceInformation.Name, deviceInformation.Id)).ToList();
            return new ReadOnlyCollection<Microphone>(microphones);
#endif
            throw new NotImplementedException();
        }

        private void AudioCaptureOnBufferReady(IBuffer buffer)
        {
            var data = buffer.ToArray();
            _bufferQueue.Enqueue(data);
            Task.Factory.StartNew(()=>OnBufferReady(new EventArgs()));
        }
    }
}
