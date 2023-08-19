using DbgCensus.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapLeaderboardGen.DependencyInjection
{
    internal class CensusConfigContainer
    {
        private CensusQueryOptions? queryOptions;

        public void SetQueryOptions(CensusQueryOptions options)
        {
            queryOptions = options;
        }

        public CensusQueryOptions GetQueryOptions()
        {
            if (queryOptions == null)
            {
                throw new InvalidOperationException("The Census query options must be set before attempting to retrieve them.");
            }
            return queryOptions;
        }
    }
}
