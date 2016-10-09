using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;

namespace DQueue.Consumer
{
    public class IocContainer
    {
        static CompositionContainer _container;

        static IocContainer()
        {
            var binPath = GetBinPath();
            var binCatalog = new DirectoryCatalog(binPath, "*.dll");

            var exeAssembly = Assembly.GetExecutingAssembly();
            var exeAssemblyCatalog = new AssemblyCatalog(exeAssembly);

            var aggregateCatalog = new AggregateCatalog(binCatalog, exeAssemblyCatalog);
            _container = new CompositionContainer(aggregateCatalog);
        }

        private static string GetBinPath()
        {
            var binPath = AppDomain.CurrentDomain.BaseDirectory;

            if (HttpContext.Current.IsAvailable())
            {
                binPath = Path.Combine(binPath, "bin");
            }

            return binPath;
        }

        public static IEnumerable<TExport> GetExports<TExport>()
        {
            return _container.GetExportedValues<TExport>().ToList();
        }

        public static IEnumerable<IConsumerService> GetConsumerServices()
        {
            return GetExports<IConsumerService>();
        }
    }
}
