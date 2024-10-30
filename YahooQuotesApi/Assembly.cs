global using Microsoft.Extensions.Logging;
global using NodaTime;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
using System.Runtime.CompilerServices;

//Action<HttpStandardResilienceOptions> is not CLS-compliant
[assembly: CLSCompliant(true)]

[assembly: InternalsVisibleTo("YahooQuotesApi.Test")]
