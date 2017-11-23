using Discord;
using Discord.Commands;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SassV2.Commands
{
	public class ServerConfigCommand : ModuleBase<SocketCommandContext>
	{
		private DiscordBot _bot;

		public ServerConfigCommand(DiscordBot bot)
		{
			_bot = bot;
		}

		[SassCommand(
			name: "config",
			desc: "get a list of server config items",
			usage: "config",
			category: "Administration"
		)]
		[RequireContext(ContextType.Guild)]
		[Command("config")]
		public async Task GetConfigItems()
		{
			var fields = typeof(ServerConfig).GetFields().Where((f) => f.IsPublic);
			await ReplyAsync("Possible config options are: " + string.Join(", ", fields.Select((f) => Util.ToSnakeCase(f.Name))));
		}

		[SassCommand(
			name: "get config",
			desc: "get a server config item",
			usage: "get config <key>",
			category: "Administration",
			example: "get config civility"
		)]
		[Command("get config")]
		[RequireContext(ContextType.Guild)]
		public async Task GetConfig(string key)
		{
			var config = _bot.Database(Context.Guild.Id).GetOrCreateObject("config:server", () => new ServerConfig());
			var fields = typeof(ServerConfig).GetFields().Where((f) => f.IsPublic);

			var keyNameReal = "";
			FieldInfo fieldReal = null;
			foreach(var field in fields)
			{
				if(Util.ToSnakeCase(field.Name) == key)
				{
					keyNameReal = field.Name;
					fieldReal = field;
					break;
				}
			}

			if(keyNameReal == "")
			{
				await ReplyAsync($"Unknown config option '{key}'.");
				return;
			}

			await ReplyAsync($"Value for {key} is {fieldReal.GetValue(config)}.");
		}

		[SassCommand(
			name: "set config",
			desc: "set a server config item",
			usage: "set config <key> <value>",
			category: "Administration",
			example: "set config civility on"
		)]
		[RequireContext(ContextType.Guild)]
		[Command("set config")]
		public async Task SetConfig(string key, [Remainder] string value)
		{
			if(!(Context.Message.Author as IGuildUser).IsAdmin(_bot))
			{
				await ReplyAsync(Util.Locale(_bot.Language(Context.Guild?.Id), "generic.notAdmin"));
				return;
			}

			var config = _bot.Database(Context.Guild.Id).GetOrCreateObject("config:server", () => new ServerConfig());
			var fields = typeof(ServerConfig).GetFields().Where((f) => f.IsPublic);

			var keyNameReal = "";
			FieldInfo fieldReal = null;
			foreach(var field in fields)
			{
				if(Util.ToSnakeCase(field.Name) == key)
				{
					keyNameReal = field.Name;
					fieldReal = field;
					break;
				}
			}

			if(keyNameReal == "")
			{
				await ReplyAsync($"Unknown config option '{key}'.");
				return;
			}

			object newValue = null;
			if(fieldReal.FieldType == typeof(bool))
			{
				newValue = Util.ParseBool(value);
			}
			else if(fieldReal.FieldType == typeof(string))
			{
				newValue = value;
			}
			else if(fieldReal.FieldType == typeof(int))
			{
				newValue = int.Parse(value);
			}
			else
			{
				newValue = fieldReal.GetValue(config);
			}

			fieldReal.SetValue(config, newValue);
			_bot.Database(Context.Guild.Id).InsertObject("config:server", config);
			await ReplyAsync($"Set '{key}' to {newValue.ToString()}.");
		}
	}

	public class ServerConfig
	{
		public bool Civility = false;

		public static ServerConfig Get(DiscordBot bot, ulong serverId)
		{
			return bot.Database(serverId).GetOrCreateObject("config:server", () => new ServerConfig());
		}
	}
}
