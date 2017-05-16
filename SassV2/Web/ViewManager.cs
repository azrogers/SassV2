using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RazorLight;

namespace SassV2.Web
{
	public class ViewManager
	{
		private IRazorLightEngine _razor;

		public ViewManager()
		{
			_razor = EngineFactory.CreatePhysical(Path.Combine(Directory.GetCurrentDirectory(), "Views"));
		}

		public string RenderView(string name, ExpandoObject values)
		{
			return _razor.Parse<object>(name + ".html", new { }, values);
		}

		public string RenderView<T>(string name, T model, ExpandoObject values)
		{
			return _razor.Parse<T>(name + ".html", model, values);
		}
	}
}
