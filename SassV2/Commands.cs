using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SassV2.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SassV2
{
	/// <summary>
	/// Registers and calls commands.
	/// </summary>
	public class CommandHandler
	{
#if DEBUG
		public const string BOT_NAME = "!sass";
#else
		public const string BOT_NAME = "sass";
#endif
		private CommandService _commands;
		private SassCommandAttribute[] _commandAttributes;
		private Logger _logger;
		private Dictionary<string, SassCommandAttribute> _commandMap;

		/// <summary>
		/// Attributes on every SASS command.
		/// </summary>
		public SassCommandAttribute[] CommandAttributes => _commandAttributes;

		public CommandHandler()
		{
			_logger = LogManager.GetCurrentClassLogger();
		}

		/// <summary>
		/// Discovers commands in SASS.
		/// </summary>
		public async Task InitCommands()
		{
			var config = new CommandServiceConfig()
			{
				CaseSensitiveCommands = false,
				ThrowOnError = true,
				DefaultRunMode = RunMode.Async
			};
			_commands = new CommandService(config);
			_commands.AddTypeReader<IGuild>(new GuildTypeReader());

			// look for classes under Commands namespace
			var commandNamespace = typeof(CommandHandler).Namespace + ".Commands";
			var modules = typeof(CommandHandler).GetTypeInfo().Assembly.GetTypes()
				.Where(t => t.Namespace == commandNamespace && t.GetTypeInfo().IsClass && t.GetTypeInfo().IsVisible);

			// look for all methods with a SassCommandAttribute
			var attributes = new List<SassCommandAttribute>();
			foreach(var module in modules)
			{
				var methods = module.GetTypeInfo().DeclaredMethods.Where(m => m.IsPublic);
				foreach(var method in methods)
				{
					var attr = method.GetCustomAttribute<SassCommandAttribute>();
					if(attr == null)
					{
						continue;
					}

					attributes.Add(attr);
				}

				await _commands.AddModuleAsync(module);
			}

			_commandAttributes = attributes.ToArray();
			_commandMap = new Dictionary<string, SassCommandAttribute>();
			foreach(var cmd in _commandAttributes)
			{
				foreach(var name in cmd.Names)
				{
					_commandMap[name.ToLower()] = cmd;
				}
			}
		}

		/// <summary>
		/// Handles a message that might be a command.
		/// </summary>
		public async Task HandleCommand(ServiceProvider services, SocketMessage messageParam)
		{
			var message = messageParam as SocketUserMessage;
			// not a SocketUserMessage
			if(message == null) return;
			// not a command
			if(!message.Content.StartsWith(BOT_NAME, StringComparison.CurrentCultureIgnoreCase)) return;

			var commandContext = new SocketCommandContext(services.GetService<DiscordBot>().Client, message);

			// hey, a command!
			if(message.Channel is IGuildChannel)
				await ActivityManager.UpdateActivity(services.GetService<DiscordBot>(), message.GuildId());

			var result = await _commands.ExecuteAsync(commandContext, BOT_NAME.Length + 1, services);
			if(!result.IsSuccess)
			{
				// print errors
				if(result.Error.Value == CommandError.Exception)
					_logger.Error(result.ErrorReason);

				// if they got it wrong, help them out a bit instead of just telling them they did it wrong.
				if(result.Error.Value == CommandError.BadArgCount)
				{
					var helpLink = HelpCommand.GetHelpLink(services.GetService<DiscordBot>(), message.Content.Substring(BOT_NAME.Length + 1));
					await commandContext.Channel.SendMessageAsync("That's not how you use that command. Check out the docs: " + helpLink);
					return;
				}

				var msg = Util.CommandErrorToMessage(result.Error.Value);
				// censor string if civility is enabled
				if(message.Channel is IGuildChannel &&
					ServerConfig.Get(services.GetService<DiscordBot>(), message.GuildId()).Civility)
					msg = Util.CivilizeString(msg);

				await commandContext.Channel.SendMessageAsync(msg);
			}
		}

		/// <summary>
		/// Finds the best command match for the input text.
		/// </summary>
		public SassCommandAttribute FindBestMatch(string text)
		{
			text = text.ToLower();

			var result =
				_commandMap.Keys
				.Where(n => text.StartsWith(n))
				.OrderByDescending(n => n.Length)
				.FirstOrDefault();

			if(result == default(string))
				return null;

			return _commandMap[result];
		}
	}

	/// <summary>
	/// Annotates a given command function with information about usage.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class SassCommandAttribute : Attribute
	{
		/// <summary>
		/// Name(s) that can be used to call this command.
		/// </summary>
		public string[] Names;

		/// <summary>
		/// What this command does.
		/// </summary>
		public string Description;

		/// <summary>
		/// How to make it do something.
		/// </summary>
		public string Usage;

		/// <summary>
		/// What type of command is this?
		/// </summary>
		public string Category;

		/// <summary>
		/// An example of using this command.
		/// </summary>
		public string Example;

		/// <summary>
		/// Should this command be shown on the help page?
		/// </summary>
		public bool Hidden;

		/// <summary>
		/// Is this a PM-only function?
		/// </summary>
		public bool IsPM = false;

		/// <summary>
		/// Convert the first name of this command to snake case.
		/// </summary>
		public string SnakeName => Util.ToSnakeCase(Names[0]);

		/// <summary>
		/// Return all names of this command as snake case.
		/// </summary>
		public IEnumerable<string> SnakeNames => Names.Select(n => Util.ToSnakeCase(n));

		/// <summary>
		/// Format the usage instructions for the web.
		/// </summary>
		public string WebUsage => Util.NewLineToLineBreak(Util.SanitizeHTML(Usage) ?? Names[0]);

		public SassCommandAttribute(
			string[] names,
			string desc = "",
			string usage = "",
			string category = "General",
			string example = "",
			bool hidden = false,
			bool isPM = false)
		{
			Names = names;
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
			Example = example;
		}
		public SassCommandAttribute(
			string name,
			string desc = "",
			string usage = "",
			string category = "General",
			string example = "",
			bool hidden = false,
			bool isPM = false)
		{
			Names = new string[] { name };
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
			Example = example;
		}

		public override string ToString() => $"Command ({Names[0]})";
	}

	public class CommandException : Exception
	{
		public CommandException(string message)
			: base(message)
		{

		}
	}

	public class GuildTypeReader : TypeReader
	{
		public override async Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider serviceProvider)
		{
			if(!ulong.TryParse(input, out var guildId))
			{
				return TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a ulong.");
			}

			var guild = await context.Client.GetGuildAsync(guildId);
			if(guild == null)
			{
				return TypeReaderResult.FromError(CommandError.ObjectNotFound, "Could not find guild with that ID.");
			}

			return TypeReaderResult.FromSuccess(guild);
		}
	}
}
