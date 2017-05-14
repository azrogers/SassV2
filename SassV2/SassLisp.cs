using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using Stringes;
using Discord;

namespace SassV2
{
	public static class SassLisp
	{
		/// <summary>
		/// Runs a lisp program.
		/// </summary>
		/// <param name="program">The program to run.</param>
		/// <returns>The final value.</returns>
		public static LispSandbox.LispValue Run(string program)
		{
			return new LispSandbox().Run(program);
		}

		/// <summary>
		/// Runs a lisp program with values gathered from the Message.
		/// </summary>
		/// <param name="msg">The message to populate variables from.</param>
		/// <param name="program">The program to run.</param>
		/// <returns>The final value.</returns>
		public static LispSandbox.LispValue Run(Message msg, string program)
		{
			var sandbox = new LispSandbox();
			sandbox.SetVariable("nickname", msg.User.NicknameOrDefault());
			sandbox.SetVariable("userid", msg.User.Id.ToString());
			sandbox.SetVariable("channel", msg.Channel.Name);
			sandbox.SetVariable("channelid", msg.Channel.Id.ToString());
			sandbox.SetVariable("message", msg.Text);
			return sandbox.Run(program);
		}

		public static LispSandbox.LispAction Compile(string program)
		{
			var sandbox = new LispSandbox();
			sandbox.SetVariable("nickname", "");
			sandbox.SetVariable("userid", "");
			sandbox.SetVariable("channel", "");
			sandbox.SetVariable("channelid", "");
			sandbox.SetVariable("message", "");
			return sandbox.Compile(program);
		}
	}

	public class LispSandbox
	{
		private Dictionary<string, LispAction> _variables = new Dictionary<string, LispAction>();

		public void SetVariable(string name, LispAction value)
		{
			_variables[name] = value;
		}

		public void SetVariable(string name, string value)
		{
			SetVariable(name, MakeActionFromValue(new LispStringValue(value)));
		}

		public LispAction MakeActionFromValue(LispValue val)
		{
			return new LispAction(val.Type, () => val);
		}

		public LispValue Run(string program)
		{
			var action = Compile(program);
			return action.Run();
		}

		public LispAction Compile(string program)
		{
			var lexer = new LispLexer();
			var reader = new LispLexer.TokenReader("program", lexer.Tokenize(program));

			return CompileSection(reader);
		}

		private LispAction CompileSection(LispLexer.TokenReader reader)
		{
			var token = reader.ReadToken();

			switch(token.ID)
			{
				case R.LeftParen:
					{
						var function = reader.ReadToken();
						var arguments = new List<LispAction>();

						while(reader.PeekLooseToken().ID != R.RightParen)
						{
							arguments.Add(CompileSection(reader));
						}

						reader.ReadLooseToken();

						var type = LispFunctions.GetFunctionType(function.Value, arguments);
						return new LispAction(type, () => LispFunctions.RunFunction(function.Value, arguments));
					}
				case R.Number:
					{
						double val;
						Util.ParseDouble(token.Value, out val);
						return new LispAction(ValueType.Number, () => new LispNumberValue(val));
					}
				case R.String:
					{
						return new LispAction(ValueType.String, () => new LispStringValue(token.Value.Trim('"')));
					}
				case R.Text:
					{
						if(token.Value == "true" || token.Value == "false")
						{
							return new LispAction(ValueType.Boolean, () => new LispBooleanValue(token.Value == "true"));
						}

						if(!_variables.ContainsKey(token.Value)) throw new LispCompilerException("program", token, "unknown variable");
						return _variables[token.Value];
					}
				default:
					throw new LispCompilerException("program", token, "unknown token");
			}
		}

		public static class LispFunctions
		{
			private static Dictionary<string, MethodInfo> _methods;

			static LispFunctions()
			{
				CreateMethodDict();
			}

			public static LispValue RunFunction(string name, IEnumerable<LispAction> arguments)
			{
				var mangledName = MangleFunctionName(name, arguments.Select(v => v.Type).ToArray());

				if(!_methods.ContainsKey(mangledName))
				{
					throw new Exception("Unknown function " + name);
				}

				var attr = _methods[mangledName].GetCustomAttribute<LispFunctionAttribute>();

				var argContainers = new List<object>();
				foreach(var arg in arguments)
				{
					switch(arg.Type)
					{
						case ValueType.Boolean:
							argContainers.Add(new A<LispBooleanValue>(arg));
							break;
						case ValueType.Function:
							argContainers.Add(new A<LispFunctionValue>(arg));
							break;
						case ValueType.List:
							argContainers.Add(new A<LispListValue>(arg));
							break;
						case ValueType.Number:
							argContainers.Add(new A<LispNumberValue>(arg));
							break;
						case ValueType.String:
							argContainers.Add(new A<LispStringValue>(arg));
							break;
						default:
							throw new Exception("unknown argument type");
					}
				}

				return (LispValue)_methods[mangledName].Invoke(null, argContainers.ToArray());
			}

			public static ValueType GetFunctionType(string name, IEnumerable<LispAction> arguments)
			{
				var types = arguments.Select(a => a.Type).ToArray();
				var mangledName = MangleFunctionName(name, types);
				if(!_methods.ContainsKey(mangledName))
					throw new Exception("unknown function " + name + " with signature " + string.Join(", ", arguments.Select(a => a.Type)));
				return _methods[mangledName].GetCustomAttribute<LispFunctionAttribute>().ReturnType;
			}

			private static void CreateMethodDict()
			{
				var classType = typeof(LispFunctions);
				var methods = classType.GetMethods().Where(m => m.GetCustomAttributes<LispFunctionAttribute>().Any());
				_methods = new Dictionary<string, MethodInfo>();
				foreach(var method in methods)
				{
					var a = method.GetCustomAttribute<LispFunctionAttribute>();
					_methods[MangleFunctionName(a.Name, a.ArgumentTypes)] = method;
				}
			}

			private static string MangleFunctionName(string name, ValueType[] args)
			{
				return name + "$$$" + string.Join("", args);
			}

			[LispFunction("+", ValueType.Number, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispNumberValue Add(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispNumberValue(n1.V.Value + n2.V.Value);

			[LispFunction("-", ValueType.Number, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispNumberValue Subtract(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispNumberValue(n1.V.Value - n2.V.Value);

			[LispFunction("*", ValueType.Number, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispNumberValue Multiply(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispNumberValue(n1.V.Value * n2.V.Value);

			[LispFunction("/", ValueType.Number, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispNumberValue Divide(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispNumberValue(n1.V.Value / n2.V.Value);

			[LispFunction("+", ValueType.String, new ValueType[] { ValueType.String, ValueType.String })]
			public static LispStringValue CombineString(A<LispStringValue> s1, A<LispStringValue> s2) => new LispStringValue(s1.V.Value + s2.V.Value);

			[LispFunction(">", ValueType.Boolean, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispBooleanValue Gt(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispBooleanValue(n1.V.Value > n2.V.Value);

			[LispFunction("<", ValueType.Boolean, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispBooleanValue Lt(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispBooleanValue(n1.V.Value < n2.V.Value);

			[LispFunction("=", ValueType.Boolean, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispBooleanValue NumberEq(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispBooleanValue(n1.V.Value == n2.V.Value);

			[LispFunction("=", ValueType.Boolean, new ValueType[] { ValueType.String, ValueType.String })]
			public static LispBooleanValue StringEq(A<LispStringValue> n1, A<LispStringValue> n2) => new LispBooleanValue(n1.V.Value == n2.V.Value);

			[LispFunction("=", ValueType.Boolean, new ValueType[] { ValueType.Boolean, ValueType.Boolean })]
			public static LispBooleanValue BooleanEq(A<LispBooleanValue> n1, A<LispBooleanValue> n2) => new LispBooleanValue(n1.V.Value == n2.V.Value);

			[LispFunction("!=", ValueType.Boolean, new ValueType[] { ValueType.Number, ValueType.Number })]
			public static LispBooleanValue NumberNeq(A<LispNumberValue> n1, A<LispNumberValue> n2) => new LispBooleanValue(n1.V.Value != n2.V.Value);

			[LispFunction("!=", ValueType.Boolean, new ValueType[] { ValueType.String, ValueType.String })]
			public static LispBooleanValue StringNeq(A<LispStringValue> n1, A<LispStringValue> n2) => new LispBooleanValue(n1.V.Value != n2.V.Value);

			[LispFunction("not", ValueType.Boolean, new ValueType[] { ValueType.Boolean })]
			public static LispBooleanValue BooleanNot(A<LispBooleanValue> n1) => new LispBooleanValue(!n1.V.Value);

			[LispFunction("and", ValueType.Boolean, new ValueType[] { ValueType.Boolean, ValueType.Boolean })]
			public static LispBooleanValue BooleanAnd(A<LispBooleanValue> n1, A<LispBooleanValue> n2) => new LispBooleanValue(n1.V.Value && n2.V.Value);

			[LispFunction("or", ValueType.Boolean, new ValueType[] { ValueType.Boolean, ValueType.Boolean })]
			public static LispBooleanValue BooleanOr(A<LispBooleanValue> n1, A<LispBooleanValue> n2) => new LispBooleanValue(n1.V.Value || n2.V.Value);


			[LispFunction("if", ValueType.Boolean, new ValueType[] { ValueType.Boolean, ValueType.Boolean, ValueType.Boolean })]
			public static LispBooleanValue IfBoolean(A<LispBooleanValue> test, A<LispBooleanValue> n1, A<LispBooleanValue> n2) =>
				new LispBooleanValue(test.V.Value ? n1.V.Value : n2.V.Value);

			[LispFunction("if", ValueType.Number, new ValueType[] { ValueType.Boolean, ValueType.Number, ValueType.Number })]
			public static LispNumberValue IfNumber(A<LispBooleanValue> test, A<LispNumberValue> n1, A<LispNumberValue> n2) =>
				new LispNumberValue(test.V.Value ? n1.V.Value : n2.V.Value);

			[LispFunction("if", ValueType.String, new ValueType[] { ValueType.Boolean, ValueType.String, ValueType.String })]
			public static LispStringValue IfString(A<LispBooleanValue> test, A<LispStringValue> n1, A<LispStringValue> n2) =>
				new LispStringValue(test.V.Value ? n1.V.Value : n2.V.Value);

			[LispFunction("includes", ValueType.Boolean, new ValueType[] { ValueType.String, ValueType.String })]
			public static LispBooleanValue StringIncludes(A<LispStringValue> haystack, A<LispStringValue> needle) => 
				new LispBooleanValue(haystack.V.Value.Contains(needle.V.Value));

			public class LispFunctionAttribute : Attribute
			{
				private ValueType _returnType;
				private ValueType[] _argumentTypes;
				private string _name;

				public ValueType ReturnType => _returnType;
				public ValueType[] ArgumentTypes => _argumentTypes;
				public string Name => _name;

				public LispFunctionAttribute(string name, ValueType returnType, ValueType[] argumentTypes)
				{
					_returnType = returnType;
					_argumentTypes = argumentTypes;
					_name = name;
				}
			}
		}

		public class A<T> where T : LispValue
		{
			private LispAction _action;

			public T V => GetValue();

			public A(LispAction action)
			{
				_action = action;
			}

			public T GetValue()
			{
				return _action.Run() as T;
			}
		}

		public class LispAction
		{
			private Func<LispValue> _func;
			private ValueType _type;

			public ValueType Type => _type;

			public LispAction(ValueType type, Func<LispValue> func)
			{
				_func = func;
				_type = type;
			}

			public LispValue Run()
			{
				return _func();
			}
		}

		public class LispValue
		{
			private ValueType _type;

			public ValueType Type => _type;

			public LispValue(ValueType type)
			{
				_type = type;
			}
		}

		public class LispStringValue : LispValue
		{
			private string _value;

			public string Value => _value;

			public LispStringValue(string val)
				: base(ValueType.String)
			{
				_value = val;
			}

			public override string ToString()
			{
				return _value;
			}
		}

		public class LispNumberValue : LispValue
		{
			private double _value;

			public double Value => _value;

			public LispNumberValue(double val)
				: base(ValueType.Number)
			{
				_value = val;
			}

			public override string ToString()
			{
				return _value.ToString();
			}
		}

		public class LispBooleanValue : LispValue
		{
			private bool _value;

			public bool Value => _value;

			public LispBooleanValue(bool val)
				: base(ValueType.Boolean)
			{
				_value = val;
			}

			public override string ToString()
			{
				return _value.ToString();
			}
		}

		public class LispListValue : LispValue
		{
			private LispValue[] _values;

			public LispValue[] Values => _values;

			public LispListValue(LispValue[] values)
				: base(ValueType.List)
			{
				_values = values;
			}
		}

		public class LispFunctionValue : LispValue
		{
			private LispAction _action;

			public LispAction Action => _action;

			public LispFunctionValue(LispAction action)
				: base(ValueType.Function)
			{
				_action = action;
			}
		}

		public enum ValueType
		{
			String,
			Number,
			Boolean,
			List,
			Function
		}
	}

	public class LispLexer
	{
		private static readonly Lexer<R> _lexer = new Lexer<R>
		{
			{ "(", R.LeftParen },
			{ ")", R.RightParen },
			{
				reader =>
				{
					reader.Eat('-');
					if(!reader.EatWhile(char.IsDigit)) return false;
					return !reader.Eat('.') || reader.EatWhile(char.IsDigit);
				},
				R.Number
			},
			{ new Regex(@"\s"), R.Whitespace },
			{
				reader =>
				{
					if(!reader.Eat('"')) return false;
					reader.EatWhile((char c) => c != '"');
					reader.Eat('"');
					return true;
				},
				R.String
			},
			{
				reader =>
				{
					if(!reader.EatWhile(char.IsLetter)) return false;
					reader.EatWhile(c => char.IsLetterOrDigit(c) || c == '_');
					return true;
				},
				R.Text
			}
		}.Ignore(R.Whitespace);

		static LispLexer()
		{
			_lexer.AddEndToken(R.EOF);
			_lexer.AddUndefinedCaptureRule(R.Text, s => s.LeftPadded ? s.TrimStart() : s.TrimEnd());
		}

		public IEnumerable<Token<R>> Tokenize(string text)
		{
			return _lexer.Tokenize(text);
		}

		internal class TokenReader
		{
			private readonly string _sourceName;
			private readonly Token<R>[] _tokens;
			private int _pos;

			public TokenReader(string sourceName, IEnumerable<Token<R>> tokens)
			{
				_sourceName = sourceName;
				_tokens = tokens.ToArray();
				_pos = 0;
			}

			public string SourceName => _sourceName;

			public int Position
			{
				get { return _pos; }
				set { _pos = value; }
			}

			/// <summary>
			/// Determines whether the reader has reached the end of the token stream.
			/// </summary>
			public bool End => _pos >= _tokens.Length;

			/// <summary>
			/// The last token that was read.
			/// </summary>
			public Token<R> PrevToken => _pos == 0 ? null : _tokens[_pos - 1];

			/// <summary>
			/// The last non-whitespace token before the current reader position.
			/// </summary>
			public Token<R> PrevLooseToken
			{
				get
				{
					if(_pos == 0) return null;
					int tempPos = _pos - 1;
					while(tempPos > 0 && _tokens[tempPos].ID == R.Whitespace)
						tempPos--;
					return _tokens[tempPos].ID != R.Whitespace ? _tokens[tempPos] : null;
				}
			}

			/// <summary>
			/// Reads the next available token.
			/// </summary>
			/// <returns></returns>
			public Token<R> ReadToken()
			{
				if(End) throw new LispCompilerException(_sourceName, null, "Unexpected end of file.");
				return _tokens[_pos++];
			}

			/// <summary>
			/// Returns the next available token, but does not consume it.
			/// </summary>
			/// <returns></returns>
			public Token<R> PeekToken()
			{
				return End ? null : _tokens[_pos];
			}

			/// <summary>
			/// Returns the next available non-whitespace token, but does not consume it.
			/// </summary>
			/// <returns></returns>
			public Token<R> PeekLooseToken()
			{
				if(End) throw new LispCompilerException(_sourceName, null, "Unexpected end of file.");
				int pos = _pos;
				SkipSpace();
				var token = _tokens[_pos];
				_pos = pos;
				return token;
			}

			/// <summary>
			/// Returns the type of the next available token.
			/// </summary>
			/// <returns></returns>
			public R PeekType() => End ? R.EOF : _tokens[_pos].ID;

			/// <summary>
			/// Determines whether the next token is of the specified type.
			/// </summary>
			/// <param name="type">The type to check for.</param>
			/// <returns></returns>
			public bool IsNext(R type)
			{
				return !End && _tokens[_pos].ID == type;
			}

			/// <summary>
			/// The last non-whitespace token type.
			/// </summary>
			public R LastNonSpaceType
			{
				get
				{
					if(_pos == 0) return R.EOF;
					R id;
					for(int i = _pos; i >= 0; i--)
					{
						if((id = _tokens[i].ID) != R.Whitespace) return id;
					}
					return R.EOF;
				}
			}

			/// <summary>
			/// Consumes the next token if its type matches the specified type. Returns true if it matches.
			/// </summary>
			/// <param name="type">The type to consume.</param>
			/// <param name="allowEof">Allow end-of-file tokens. Specifying False will throw an exception instead.</param>
			/// <returns></returns>
			public bool Take(R type, bool allowEof = true)
			{
				if(End)
				{
					if(!allowEof)
						throw new LispCompilerException(_sourceName, null, "Unexpected end-of-file.");
					return false;
				}
				if(_tokens[_pos].ID != type) return false;
				_pos++;
				return true;
			}

			/// <summary>
			/// Consumes the next non-whitespace token if its type matches the specified type. Returns true if it matches.
			/// </summary>
			/// <param name="type">The type to consume.</param>
			/// <param name="allowEof">Allow end-of-file tokens. Specifying False will throw an exception instead.</param>
			/// <returns></returns>
			public bool TakeLoose(R type, bool allowEof = true)
			{
				if(End)
				{
					if(!allowEof)
						throw new LispCompilerException(_sourceName, null, "Unexpected end-of-file.");
					return false;
				}
				SkipSpace();
				if(_tokens[_pos].ID != type) return false;
				_pos++;
				SkipSpace();
				return true;
			}

			/// <summary>
			/// Consumes the next token if its type matches any of the specified types. Returns true if a match was found.
			/// </summary>
			/// <param name="types">The types to consume.</param>
			/// <returns></returns>
			public bool TakeAny(params R[] types)
			{
				if(End) return false;
				foreach(var type in types)
				{
					if(_tokens[_pos].ID != type) continue;
					_pos++;
					return true;
				}
				return false;
			}

			/// <summary>
			/// Consumes the next token if its type matches any of the specified types, and outputs the matching type. Returns true if a match was found.
			/// </summary>
			/// <param name="result">The matched type.</param>
			/// <param name="types">The types to consume.</param>
			/// <returns></returns>
			public bool TakeAny(out R result, params R[] types)
			{
				result = default(R);
				if(End) return false;
				foreach(var type in types)
				{
					if(_tokens[_pos].ID != type) continue;
					result = type;
					_pos++;
					return true;
				}
				return false;
			}

			/// <summary>
			/// Consumes the next non-whitespace token if its type matches any of the specified types. Returns true if a match was found.
			/// </summary>
			/// <param name="types">The types to consume.</param>
			/// <returns></returns>
			public bool TakeAnyLoose(params R[] types)
			{
				if(End) return false;
				SkipSpace();
				foreach(var type in types)
				{
					if(_tokens[_pos].ID != type) continue;
					_pos++;
					SkipSpace();
					return true;
				}
				return false;
			}

			/// <summary>
			/// Consumes the next non-whitespace token if its type matches any of the specified types, and outputs the matching type. Returns true if a match was found.
			/// </summary>
			/// <param name="result">The matched type.</param>
			/// <param name="types">The types to consume.</param>
			/// <returns></returns>
			public bool TakeAnyLoose(out R result, params R[] types)
			{
				result = default(R);
				if(End) return false;
				SkipSpace();
				foreach(var type in types)
				{
					if(_tokens[_pos].ID != type) continue;
					result = type;
					_pos++;
					SkipSpace();
					return true;
				}
				return false;
			}

			/// <summary>
			/// Consumes as many tokens as possible, as long as they match the specified type. Returns true if at least one was found.
			/// </summary>
			/// <param name="type">The type to consume.</param>
			/// <param name="allowEof">Allow end-of-file tokens. Specifying False will throw an exception instead.</param>
			/// <returns></returns>
			public bool TakeAll(R type, bool allowEof = true)
			{
				if(End)
				{
					if(!allowEof)
						throw new LispCompilerException(_sourceName, null, "Unexpected end-of-file.");
					return false;
				}
				if(_tokens[_pos].ID != type) return false;
				do
				{
					_pos++;
				} while(!End && _tokens[_pos].ID == type);
				return true;
			}

			/// <summary>
			/// Consumes as many non-whitespace tokens as possible, as long as they match the specified type. Returns true if at least one was found.
			/// </summary>
			/// <param name="type">The type to consume.</param>
			/// <param name="allowEof">Allow end-of-file tokens. Specifying False will throw an exception instead.</param>
			/// <returns></returns>
			public bool TakeAllLoose(R type, bool allowEof = true)
			{
				if(End)
				{
					if(!allowEof)
						throw new LispCompilerException(_sourceName, null, "Unexpected end-of-file.");
					return false;
				}
				SkipSpace();
				if(_tokens[_pos].ID != type) return false;
				do
				{
					SkipSpace();
					_pos++;
				} while(!End && _tokens[_pos].ID == type);
				return true;
			}

			/// <summary>
			/// Reads and returns the next token if its type matches the specified type.
			/// If it does not match, a LispCompilerException is thrown with the expected token name.
			/// </summary>
			/// <param name="type">The token type to read.</param>
			/// <param name="expectedTokenName">A display name describing what the token is for.</param>
			/// <returns></returns>
			public Token<R> Read(R type, string expectedTokenName = null)
			{
				if(End)
					throw new LispCompilerException(_sourceName, null, "Expected " + (expectedTokenName ?? "'" + _lexer.GetSymbolForId(type) + "'") + ", but hit end of file.");
				if(_tokens[_pos].ID != type)
				{
					throw new LispCompilerException(_sourceName, _tokens[_pos], "Expected " + (expectedTokenName ?? "'" + _lexer.GetSymbolForId(type) + "'"));
				}
				return _tokens[_pos++];
			}

			/// <summary>
			/// Reads and returns the next token if its type matches any of the given types
			/// If it does not match, a LispCompilerException is thrown with the expected token names.
			/// </summary>
			/// <param name="types">The token types accepted for the read token.</param>
			/// <returns></returns>
			public Token<R> ReadAny(params R[] types)
			{
				if(End)
					throw new LispCompilerException(_sourceName, null,
						$"Expected any from {{{String.Join(", ", types.Select(t => _lexer.GetSymbolForId(t)).ToArray())}}}, but hit end of file.");

				if(!types.Contains(_tokens[_pos].ID)) // NOTE: .Contains isn't too fast but does it matter in this case?
					throw new LispCompilerException(_sourceName, _tokens[_pos],
						$"Expected any from {{{String.Join(", ", types.Select(t => _lexer.GetSymbolForId(t)).ToArray())}}}.");

				return _tokens[_pos++];
			}

			/// <summary>
			/// Reads and returns the next non-whitespace token if its type matches the specified type.
			/// If it does not match, a LispCompilerException is thrown with the expected token name.
			/// </summary>
			/// <param name="type">The token type to read.</param>
			/// <param name="expectedTokenName">A display name describing what the token is for.</param>
			/// <returns></returns>
			public Token<R> ReadLoose(R type, string expectedTokenName = null)
			{
				if(End)
					throw new LispCompilerException(_sourceName, null, "Expected " + (expectedTokenName ?? "'" + _lexer.GetSymbolForId(type) + "'") + ", but hit end of file.");
				SkipSpace();
				if(_tokens[_pos].ID != type)
				{
					throw new LispCompilerException(_sourceName, _tokens[_pos], "Expected " + (expectedTokenName ?? "'" + _lexer.GetSymbolForId(type) + "'"));
				}
				var t = _tokens[_pos++];
				SkipSpace();
				return t;
			}

			/// <summary>
			/// Reads a series of tokens into a buffer as long as they match the types specified in an array, in the order they appear. Returns True if reading was successful.
			/// </summary>
			/// <param name="buffer">The buffer to read into.</param>
			/// <param name="offset">The offset at which to begin writing tokens into the buffer.</param>
			/// <param name="types">The required types.</param>
			/// <returns></returns>
			public bool TakeSeries(Token<R>[] buffer, int offset, params R[] types)
			{
				if(_pos >= _tokens.Length) return types.Length == 0;
				if(_tokens.Length - _pos < types.Length || buffer.Length - offset < types.Length)
					return false;
				for(int i = 0; i < types.Length; i++)
				{
					if(_tokens[_pos + i].ID != types[i]) return false;
					buffer[i + offset] = _tokens[_pos + i];
				}
				_pos += types.Length;
				return true;
			}

			/// <summary>
			/// Reads a series of non-whitespace tokens into a buffer as long as they match the types specified in an array, in the order they appear. Returns True if reading was successful.
			/// </summary>
			/// <param name="buffer">The buffer to read into.</param>
			/// <param name="offset">The offset at which to begin writing tokens into the buffer.</param>
			/// <param name="types">The required types.</param>
			/// <returns></returns>
			public bool TakeSeriesLoose(Token<R>[] buffer, int offset, params R[] types)
			{
				if(_pos >= _tokens.Length) return types.Length == 0;
				if(_tokens.Length - _pos < types.Length || buffer.Length - offset < types.Length)
					return false;
				int i = 0;
				int j = 0;
				while(i < types.Length)
				{
					if(_pos + j >= _tokens.Length) return false;
					while(_tokens[_pos + j].ID == R.Whitespace) j++;
					if(_pos + j >= _tokens.Length) return false;

					if(_tokens[_pos + j].ID != types[i]) return false;
					buffer[i + offset] = _tokens[_pos + i];
					j++;
					i++;
				}
				_pos += j;
				return true;
			}

			/// <summary>
			/// Reads and returns the next non-whitespace token.
			/// </summary>
			/// <returns></returns>
			public Token<R> ReadLooseToken()
			{
				if(End)
					throw new LispCompilerException(_sourceName, null, "Expected token, but hit end of file.");
				SkipSpace();
				var token = _tokens[_pos++];
				SkipSpace();
				return token;
			}

			/// <summary>
			/// Consumes as many token as possible while they satisfy the specified predicate.
			/// </summary>
			/// <param name="predicate">The predicate to test tokens with.</param>
			/// <param name="allowEof">Allow end-of-file tokens. Specifying False will throw an exception instead.</param>
			/// <returns></returns>
			public bool TakeAllWhile(Func<Token<R>, bool> predicate, bool allowEof = true)
			{
				if(predicate == null) throw new ArgumentNullException(nameof(predicate));
				if(End)
				{
					if(!allowEof)
						throw new LispCompilerException(_sourceName, null, "Unexpected end-of-file.");
					return false;
				}

				int i = _pos;
				Token<R> t;
				while(_pos < _tokens.Length)
				{
					t = _tokens[_pos];
					if(!predicate(t))
					{
						return _pos > i;
					}
					_pos++;
				}
				return true;
			}

			public bool SkipSpace() => TakeAll(R.Whitespace);

			public Token<R> this[int pos] => _tokens[pos];
		}
	}

	public enum R
	{
		LeftParen,
		RightParen,
		Number,
		Whitespace,
		Boolean,
		String,
		Variable,
		Text,
		EOF
	}

	public class LispCompilerException : Exception
	{
		/// <summary>
		/// The line on which the error occurred.
		/// </summary>
		public int Line { get; private set; }
		/// <summary>
		/// The column on which the error occurred.
		/// </summary>
		public int Column { get; private set; }
		/// <summary>
		/// The character index on which the error occurred.
		/// </summary>
		public int Index { get; private set; }
		/// <summary>
		/// The length of the token(s) on which the error occurred.
		/// </summary>
		public int Length { get; private set; }

		internal LispCompilerException(string name, Stringe source, string message)
			: base(source != null
				  ? $"{name} @ Line {source.Line}, Col {source.Column}: {message}"
				  : $"{name}: {message}")
		{
			if(source == null) return;
			Line = source.Line;
			Column = source.Column;
			Index = source.Offset;
			Length = source.Length;
		}

		internal LispCompilerException(string name, Stringe source, Exception innerException)
			: base(source != null
				  ? $"{name} @ Line {source.Line}, Col {source.Column}: {innerException.Message}"
				  : $"{name}: {innerException.Message}",
				  innerException)
		{
			if(source == null) return;
			Line = source.Line;
			Column = source.Column;
			Index = source.Offset;
			Length = source.Length;
		}
	}
}