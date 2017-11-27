using System.Collections.Generic;
using System.IO;

namespace SassV2
{
	/// <summary>
	/// Decodes Discord Streaming File.
	/// The concept here is it's a file that contains pre-encoded opus buffers
	/// that can be sent verbatim to Discord without re-encoding. This enables
	/// these files to be streamed with pretty much no overhead. However, the
	/// advantages over just using any other format aren't really that important
	/// and Sass doesn't have streaming functionality anymore anyways.
	/// </summary>
	public class DSFile
	{
		public const uint MAGIC_NUMBER = 0x46465344;

		/// <summary>
		/// Key value metadata of this file.
		/// </summary>
		public Dictionary<string, string> Metadata = new Dictionary<string, string>();
		/// <summary>
		/// All opus buffers contained within this file.
		/// </summary>
		public byte[][] Buffers;

		private string _file;
		private BinaryReader _reader;

		public DSFile(string file)
		{
			_file = file;
			_reader = new BinaryReader(File.Open(_file, FileMode.Open));

			ReadFile();
		}

		~DSFile()
		{
			_reader.Dispose();
		}

		private void ReadFile()
		{
			if(_reader.ReadUInt32() != MAGIC_NUMBER)
			{
				throw new IOException("Not a valid file.");
			}

			var numPackets = _reader.ReadInt32();

			var metadataCount = _reader.ReadByte();
			for(var i = 0; i < metadataCount; i++)
			{
				var tagLength = _reader.ReadByte();
				var tagName = new string(_reader.ReadChars(tagLength));
				var tagValueLength = _reader.ReadByte();
				var tagValue = new string(_reader.ReadChars(tagValueLength));
				Metadata[tagName] = tagValue;
			}

			var buffers = new List<byte[]>();
			for(var i = 0; i < numPackets; i++)
			{
				var length = _reader.ReadInt32();
				buffers.Add(_reader.ReadBytes(length));
			}

			Buffers = buffers.ToArray();
		}
	}
}
