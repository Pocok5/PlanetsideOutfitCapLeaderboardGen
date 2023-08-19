using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen
{
    internal class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
    {
        public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            throw new NotImplementedException();
        }

        public class Settings : CommandSettings
        {

        }
    }
}
