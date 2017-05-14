using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SassV2
{
	/// <summary>
	/// Decodes Discord Streaming File
	/// </summary>
	public class DSFile
	{
		public const uint MAGIC_NUMBER = 0x46465344;

		public Dictionary<string, string> Metadata = new Dictionary<string, string>();
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
			_reader.Close();
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
