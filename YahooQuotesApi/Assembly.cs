global using Microsoft.Extensions.Logging;
global using NodaTime;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: CLSCompliant(true)]
[assembly: InternalsVisibleTo("YahooQuotesApi.Test")]

[assembly: SuppressMessage("Usage", "CA1848:Use the LoggerMessage delegates")]
[assembly: SuppressMessage("Usage", "IDE0130: Namespace does not match folder structure")]
[assembly: SuppressMessage("Usage", "CA1000: Do not declare static members on generic types")]
[assembly: SuppressMessage("Usage", "CA1873: Evaluation of this argument may be expensive")]
[assembly: SuppressMessage("Design", "CA1034: Nested types should not be visible")]
