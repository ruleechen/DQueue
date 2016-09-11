using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DQueue.ServiceHost
{
    public class Injector
    {
        static CompositionContainer _container;

        static Injector()
        {
            // Cause if you load the assembly by using Assembly.LoadFile() the assembly will automatically be put into your CurrentDomain
            var assembiles = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll").Select(x => Assembly.LoadFile(x));

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
    }
}
