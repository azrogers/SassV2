using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SassV2;
using Discord;
using Discord.Audio;
using FlacBox;

namespace SassV2.Commands
{
	public static class OceanMan
	{
		private const int BUFFER_SIZE = 3840;

		private static byte[][] _oceanManBuffer;
		private static List<IAudioClient> _oceanManClients = new List<IAudioClient>();
		private static Dictionary<ulong, float> _oceanManVolume = new Dictionary<ulong, float>();
		private static Thread _thread;

		static OceanMan()
		{
			var file = new Wave16OverFlacStream(File.Open("Ocean Man.flac", FileMode.Open));
			
			var offset = 0;
			var buffers = new List<byte[]>();
			
			while(offset < file.Length)
			{
				var buffer = new byte[BUFFER_SIZE];
				var read = file.Read(buffer, 0, BUFFER_SIZE);
				offset += BUFFER_SIZE;
				buffers.Add(buffer);
			}
			_oceanManBuffer = buffers.ToArray();

			_thread = new Thread(OceanManThread);
			_thread.Start();
		}

		[Command(name: "ocean man", desc: "ocean man, take me by the hand", usage: "lead me to the land", category: "Dumb")]
		public static async Task<string> OceanManIn(DiscordBot bot, Message msg, string args)
		{
			var userVoiceChannel = msg.User.VoiceChannel;

			if(userVoiceChannel == null)
			{
				return "you're not in a voice channel.";
			}

			var client = await bot.Client.GetService<AudioService>().Join(userVoiceChannel);
			_oceanManVolume[userVoiceChannel.Id] = Util.LogVolume(0.1f);

			lock(_oceanManClients)
			{
				_oceanManClients.Add(client);
			}

			return "ok sure";
		}

		[Command(name: "goodbye, ocean man", desc: "don't leave me...", usage: "goodbye, ocean man", category: "Dumb")]
		public static async Task<string> OceanManOut(DiscordBot bot, Message msg, string args)
		{
			var userVoiceChannel = msg.User.VoiceChannel;

			if(userVoiceChannel == null)
			{
				return "you're not in a voice channel.";
			}

			var client = _oceanManClients.Where(c => c.Channel.Id == userVoiceChannel.Id).FirstOrDefault();

			if(client == default(IAudioClient))
			{
				return "there's no ocean man in your voice channel";
			}

			lock(_oceanManClients)
			{
				_oceanManClients.Remove(client);
			}

			await client.Disconnect();

			return "ok sure";
		}

		private static void OceanManThread()
		{
			var currentBuffer = 0;

			var processingStopwatch = new Stopwatch();
			processingStopwatch.Start();

			while(true)
			{
				while(!_oceanManClients.Any())
				{
					Thread.Sleep(1000);
				}

				processingStopwatch.Reset();
				var buffer = _oceanManBuffer[currentBuffer];

				lock(_oceanManClients)
				{
					foreach(var client in _oceanManClients)
					{
						if(client.State != ConnectionState.Connected)
							continue;
						var volume = _oceanManVolume[client.Channel.Id];
						var bufferCopy = new byte[BUFFER_SIZE];
						Array.Copy(buffer, bufferCopy, BUFFER_SIZE);

						for(var i = 0; i < BUFFER_SIZE; i += 2)
						{
							var val = BitConverter.ToInt16(new byte[] { buffer[i], buffer[i + 1] }, 0);
							val = (short)(val * volume);
							var bytes = BitConverter.GetBytes(val);
							buffer[i] = bytes[0];
							buffer[i + 1] = bytes[1];
						}
						client.Send(buffer, 0, buffer.Length);
					}
				}

				currentBuffer++;
				if(currentBuffer > _oceanManBuffer.Length - 1)
					currentBuffer = 0;
				
				Thread.Sleep((int)(80 - processingStopwatch.ElapsedMilliseconds));
			}
		}
	}
}
