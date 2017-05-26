using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace SassV2
{
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

		public SassCommandAttribute[] CommandAttributes => _commandAttributes;

		public CommandHandler()
		{
			_logger = LogManager.GetCurrentClassLogger();
		}

		public async Task InitCommands()
		{
			var config = new CommandServiceConfig();
			config.CaseSensitiveCommands = false;
			config.ThrowOnError = true;
			config.DefaultRunMode = RunMode.Async;

			_commands = new CommandService(config);
			_commands.AddTypeReader<IGuild>(new GuildTypeReader());
			var commandNamespace = typeof(CommandHandler).Namespace + ".Commands";
			var modules = typeof(CommandHandler).GetTypeInfo().Assembly.GetTypes()
				.Where(t => t.Namespace == commandNamespace && t.GetTypeInfo().IsClass && t.GetTypeInfo().IsVisible);

			var attributes = new List<SassCommandAttribute>();
			foreach (var module in modules)
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
		}

		public async Task HandleCommand(ServiceProvider services, SocketMessage messageParam)
		{
			var message = messageParam as SocketUserMessage;
			if (message == null) return;
			if (!message.Content.StartsWith(BOT_NAME, StringComparison.CurrentCultureIgnoreCase)) return;

			var commandContext = new SocketCommandContext(services.GetService<DiscordBot>().Client, message);

			var result = await _commands.ExecuteAsync(commandContext, BOT_NAME.Length + 1, services);
			if(!result.IsSuccess)
			{
				if(result.Error.Value == CommandError.Exception)
					_logger.Error(result.ErrorReason);
				await commandContext.Channel.SendMessageAsync(Util.CommandErrorToMessage(result.Error.Value));
			}
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class SassCommandAttribute : Attribute
	{
		public string[] Names;
		public string Description;
		public string Usage;
		public string Category;
		public bool Hidden;
		public bool IsPM = false;

		public SassCommandAttribute(string[] names, string desc = "", string usage = "", string category = "General", bool hidden = false, bool isPM = false)
		{
			Names = names;
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
		}
		public SassCommandAttribute(string name, string desc = "", string usage = "", string category = "General", bool hidden = false, bool isPM = false)
		{
			Names = new string[] { name };
			Description = desc;
			Usage = usage;
			Category = category;
			Hidden = hidden;
			IsPM = isPM;
		}
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
		public override async Task<TypeReaderResult> Read(ICommandContext context, string input)
		{
			ulong guildId;
			if(!ulong.TryParse(input, out guildId))
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
