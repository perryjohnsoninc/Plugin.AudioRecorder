using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.AudioRecorder
{
	internal class WaveRecorder : IDisposable
	{
		string audioFilePath;
		FileStream fileStream;
		StreamWriter streamWriter;
		internal BinaryWriter writer;
		long byteCount;
		IAudioStream audioStream;

		/// <summary>
		/// Starts recording WAVE format audio from the audio stream.
		/// </summary>
		/// <param name="stream">A <see cref="IAudioStream"/> that provides the audio data.</param>
		/// <param name="filePath">The full path of the file to record audio to.</param>
		public async Task StartRecorder (IAudioStream stream, string filePath)
		{
			if (stream == null)
			{
				throw new ArgumentNullException (nameof (stream));
			}

			try
			{
				//if we're restarting, let's see if we have an existing stream configured that can be stopped
				if (audioStream != null)
				{
					await audioStream.Stop ();
				}

				audioFilePath = filePath;
				audioStream = stream;

				fileStream = new FileStream (filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
				streamWriter = new StreamWriter (fileStream);
				writer = new BinaryWriter (streamWriter.BaseStream, Encoding.UTF8);

				byteCount = fileStream.Length;
				audioStream.OnBroadcast += OnStreamBroadcast;
				audioStream.OnActiveChanged += StreamActiveChanged;

				if (!audioStream.Active)
				{
					await audioStream.Start ();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine ("Error in WaveRecorder.StartRecorder(): {0}", ex.Message);

				StopRecorder ();
				throw;
			}
		}

		/// <summary>
		/// Gets a new <see cref="Stream"/> to the audio file in readonly mode.
		/// </summary>
		/// <returns>A <see cref="Stream"/> object that can be used to read the audio file from the beginning.</returns>
		public Stream GetAudioFileStream ()
		{
			//return a new stream to the same audio file, in Read mode
			return new FileStream (audioFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		}

		void StreamActiveChanged (object sender, bool active)
		{
			if (!active)
			{
				StopRecorder ();
			}
		}

		void OnStreamBroadcast (object sender, byte [] bytes)
		{
			try
			{
				if (writer != null && streamWriter != null)
				{
					writer.Write (bytes);
					byteCount += bytes.Length;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine ("Error in WaveRecorder.OnStreamBroadcast(): {0}", ex.Message);

				StopRecorder ();
			}
		}

		/// <summary>
		/// Stops recording WAV audio from the underlying <see cref="IAudioStream"/> and finishes writing the WAV file.
		/// </summary>
		public void StopRecorder ()
		{
			try
			{
				if (audioStream != null)
				{
					audioStream.OnBroadcast -= OnStreamBroadcast;
					audioStream.OnActiveChanged -= StreamActiveChanged;
				}

				if (writer != null)
				{
					if (streamWriter.BaseStream.CanWrite)
					{
						//now that audio is finished recording, write a WAV/RIFF header at the beginning of the file
						writer.Seek (0, SeekOrigin.Begin);
						AudioFunctions.WriteWavHeader (writer, audioStream.ChannelCount, audioStream.SampleRate, audioStream.BitsPerSample, byteCount);
					}

					writer.Dispose (); //this should properly close/dispose the underlying stream as well
					writer = null;
					fileStream = null;
					streamWriter = null;
				}

				audioStream = null;
			}
			catch (Exception ex)
			{
				Debug.WriteLine ("Error during StopRecorder: {0}", ex.Message);
				throw;
			}
		}


		/// <summary>
		/// Pauses recording WAV audio from the underlying <see cref="IAudioStream"/>.
		/// </summary>
		public void PauseRecorder()
		{
			try
			{
				if (audioStream != null)
				{
					audioStream.OnBroadcast -= OnStreamBroadcast;
					audioStream.OnActiveChanged -= StreamActiveChanged;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error during PauseRecorder: {0}", ex.Message);
				throw;
			}
		}


		/// <summary>
		/// Resumes recording WAV audio from the underlying <see cref="IAudioStream"/>.
		/// </summary>
		public void ResumeRecorder()
		{
			try
			{
				if (audioStream != null)
				{
					audioStream.OnBroadcast += OnStreamBroadcast;
					audioStream.OnActiveChanged += StreamActiveChanged;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error during PauseRecorder: {0}", ex.Message);
				throw;
			}
		}


		public void Seek(int offset, SeekOrigin seekOrigin = SeekOrigin.Begin)
		{
			try
			{
				if (audioStream != null)
				{
					//if (seekOrigin == SeekOrigin.Begin)
					//	byteCount = offset;
					//else if (seekOrigin == SeekOrigin.Current)
					//	byteCount = writer.BaseStream.Position + offset;
					//else if (seekOrigin == SeekOrigin.End)
					//	byteCount = writer.BaseStream.Position + offset;
					//if (byteCount > writer.BaseStream.Length)
					//{
					//	byteCount = writer.BaseStream.Length;
					//}
					//else if (byteCount < 0)
					//{
					//	byteCount = 0;
					//}

					PauseRecorder();

					writer.Seek(offset, seekOrigin);
					byteCount = writer.BaseStream.Length;

					ResumeRecorder();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error during PauseRecorder: {0}", ex.Message);
				throw;
			}
		}


		public void Dispose ()
		{
			StopRecorder ();
		}
	}
}
