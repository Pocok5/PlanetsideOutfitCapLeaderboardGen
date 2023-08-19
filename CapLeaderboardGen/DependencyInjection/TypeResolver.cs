using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DependencyInjection
{
    internal class TypeResolver: ITypeResolver
    {
        private readonly IServiceProvider serviceProvider;

        public TypeResolver(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public object? Resolve(Type? type)
        {
            if (type == null) return null;

            return serviceProvider.GetRequiredService(type);
        }
    }
}
