﻿using AngleSharp;
using Newtonsoft.Json;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SassV2
{
	public class EmoteManager
	{
		private const string EMOTES_URL = "https://forums.somethingawful.com/misc.php?action=showsmilies";
		private const string EMOTES_DIR = "emotes";
		private const string CACHE_FILE = "emotes.json";

		// a concurrent dictionary acting as a hashmap storing the list of emotes
		private static Dictionary<string, string> _emotes = new Dictionary<string, string>();
		private static Logger _logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns whether the given emote exists.
		/// </summary>
		public static bool HasEmote(string name) => _emotes.ContainsKey(name.ToLower());

		/// <summary>
		/// Returns a scaled emote image.
		/// </summary>
		/// <param name="name">The name of the emote.</param>
		/// <param name="size">The target width of the image. Height will be scaled with aspect ratio.</param>
		/// <returns>The name of the emote file along with the data.</returns>
		public static (string, byte[]) GetEmote(string name, int size)
		{
			if(size < 1 || !_emotes.ContainsKey(name))
			{
				return (null, null);
			}

			var stream = new MemoryStream();
			using(var image = Image.Load(Path.Combine(EMOTES_DIR, _emotes[name])))
			{
				// if this is a gif, attempt to scale it with ffmpeg if possible
				if(image.Frames.Count > 1)
				{
					var result = ScaleFfmpeg(name, size);
					if(result != null)
					{
						return (Util.RandomString(false) + ".gif", result);
					}
				}

				// resize height, keeping aspect ratio
				var h = (image.Height / image.Width) * size;
				image.Mutate(x => x.Resize(size, h, KnownResamplers.Box));
				var ext = ".gif";
				// save as gif if animated
				if(image.Frames.Count > 1)
				{
					var encoder = new GifEncoder()
					{
						ColorTableMode = GifColorTableMode.Global,
						Quantizer = new WuQuantizer(256)
					};

					image.SaveAsGif(stream, encoder);
				}
				else
				{
					ext = ".png";
					image.SaveAsPng(stream);
				}

				return (Util.RandomString(false) + ext, stream.ToArray());
			}
		}

		/// <summary>
		/// Updates emotes.
		/// </summary>
		public static async Task Update()
		{
			await DownloadEmotes();
			// make sure cache gets written
			lock(_emotes)
			{
				File.WriteAllText(CACHE_FILE, JsonConvert.SerializeObject(_emotes));
			}
		}

		/// <summary>
		/// Downloads each emote and stores it in the emotes folder.
		/// </summary>
		private static async Task DownloadEmotes()
		{
			var emotes = await GetEmotesList();
			if(!Directory.Exists(EMOTES_DIR))
			{
				Directory.CreateDirectory(EMOTES_DIR);
			}

			// lock emotes to update it with the current emotes list
			lock(_emotes)
			{
				_emotes.Clear();

				if(File.Exists(CACHE_FILE))
				{
					_emotes = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(CACHE_FILE));
				}
			}

			using(var client = new HttpClient())
			{
				foreach(var emote in emotes)
				{
					// don't re-download existing emotes
					if(_emotes.ContainsKey(emote.Key.ToLower()))
					{
						continue;
					}

					var name = emote.Key;
					var url = emote.Value;

					byte[] img;
					try
					{
						img = await client.GetByteArrayAsync(url);
					}
					catch(HttpRequestException)
					{
						// don't stop because the request failed - we need to make it to the end to write the cache
						_logger.Info("request failed on url: " + url);
						continue;
					}

					var ext = Path.GetExtension(url);

					// random filename
					var filename = Util.RandomString(false);
					while(File.Exists(filename + ext))
					{
						filename = Util.RandomString(false);
					}

					await File.WriteAllBytesAsync(Path.Combine(EMOTES_DIR, filename + ext), img);

					lock(_emotes)
					{
						_emotes[name.ToLower()] = filename + ext;
					}

					_logger.Info("downloaded emote " + name);
				}
			}

			lock(_emotes)
			{
				File.WriteAllText(CACHE_FILE, JsonConvert.SerializeObject(_emotes));
				_logger.Info("Finished downloading emotes.");
			}
		}

		/// <summary>
		/// Parses the SA emotes page to get the latest list of emotes
		/// </summary>
		private static async Task<Dictionary<string, string>> GetEmotesList()
		{
			var context = BrowsingContext.New(AngleSharp.Configuration.Default.WithDefaultLoader());
			var document = await context.OpenAsync(EMOTES_URL);

			var emotes = document.QuerySelectorAll(".smilie_group li.smilie");
			var emoteUrls = new Dictionary<string, string>();
			foreach(var emote in emotes)
			{
				// get emote name and url
				var name = emote.QuerySelector(".text").TextContent.Trim(':');
				var url = emote.QuerySelector("img").GetAttribute("src");
				emoteUrls[name] = url;
			}

			return emoteUrls;
		}

		private static byte[] ScaleFfmpeg(string name, int size)
		{
			var src = Path.Combine(EMOTES_DIR, _emotes[name]);
			var rand = Util.RandomString(false);
			var dest = Path.Combine(Path.GetTempPath(), rand + ".gif");
			var destPalette = Path.Combine(Path.GetTempPath(), rand + ".png");

			// no ffmpeg, fallback to imagesharp
			var ffmpeg = FindFfmpeg();
			if(ffmpeg == null)
			{
				return null;
			}

			var filter = $"scale={size}:-1:flags=neighbor";

			var startInfo = new ProcessStartInfo();
			startInfo.FileName = ffmpeg;

			// generate palette
			startInfo.Arguments = $"-i \"{src}\" -vf \"{filter},palettegen\" \"{destPalette}\"";
			Process.Start(startInfo).WaitForExit();

			if(!File.Exists(destPalette))
			{
				_logger.Error("FFMPEG failed to gen palette");
				return null;
			}

			// use palette to resize gif
			startInfo.Arguments = $"-i \"{src}\" -i \"{destPalette}\" -lavfi \"{filter} [x]; [x][1:v] paletteuse\" \"{dest}\"";
			Process.Start(startInfo).WaitForExit();

			if(!File.Exists(dest))
			{
				File.Delete(destPalette);
				_logger.Error("FFMPEG failed to create gif");
				return null;
			}

			var data = File.ReadAllBytes(dest);
			File.Delete(destPalette);
			File.Delete(dest);
			return data;
		}

		private static string FindFfmpeg()
		{
			// look through all paths in the path variable to find ffmpeg
			var values = System.Environment.GetEnvironmentVariable("PATH");
			foreach(var path in values.Split(Path.PathSeparator))
			{
				var full = Path.Combine(path, "ffmpeg");
				if(File.Exists(full))
				{
					return full;
				}
				else if(File.Exists(full + ".exe"))
				{
					return full + ".exe";
				}
			}

			return null;
		}
	}
}
