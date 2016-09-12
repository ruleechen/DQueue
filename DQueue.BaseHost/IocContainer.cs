using DQueue.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;

namespace DQueue.BaseHost
{
    public class IocContainer
    {
        static CompositionContainer _container;

        static IocContainer()
        {
            var binPath = AppDomain.CurrentDomain.BaseDirectory;

            if (HttpContext.Current.IsAvailable())
            {
                binPath = Path.Combine(binPath, "bin");
            }

            // Cause if you load the assembly by using Assembly.LoadFile() the assembly will automatically be put into your CurrentDomain
            var assembiles = Directory.GetFiles(binPath, "*.dll").Select(x => Assembly.LoadFile(x));

            var catalog = new AggregateCatalog();
            foreach (var assembily in assembiles)
            {
                var item = new AssemblyCatalog(assembily);
                catalog.Catalogs.Add(item);
            }

            _container = new CompositionContainer(catalog);
        }

        public static IEnumerable<TService> GetAllExports<TService>()
        {
            var lazy = _container.GetExports<TService>();
            return lazy.Select(x => x.Value);
        }

        public static IEnumerable<IQueueService> GetQueueServices()
        {
            return GetAllExports<IQueueService>();
        }
    }
}
