using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;

namespace SassV2
{
	public class ViewManager
	{
		private IRazorEngineService _razor;

		public ViewManager()
		{
			var config = new TemplateServiceConfiguration();
#if DEBUG
			config.Debug = true;
			config.TemplateManager = new WatchingResolvePathTemplateManager(new string[] { "Views" }, new InvalidatingCachingProvider());
#else
			config.TemplateManager = new ResolvePathTemplateManager(new string[] { "Views" });
#endif
			_razor = RazorEngineService.Create(config);
		}

		public string RenderView(string name, object values)
		{
			return _razor.RunCompile(name + ".html", null, null, Util.ViewBagFromAnonObject(values));
		}

		public string RenderView<T>(string name, T model, object values)
		{
			return _razor.RunCompile(name + ".html", typeof(T), model, Util.ViewBagFromAnonObject(values));
		}
	}
}
