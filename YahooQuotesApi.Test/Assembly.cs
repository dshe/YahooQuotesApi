global using Microsoft.Extensions.Logging;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Xunit;
global using Xunit.Abstractions;
using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(false)]

[assembly: CollectionBehavior(DisableTestParallelization = false)]
[assembly: SuppressMessage("Usage", "IDE0130: Namespace does not match folder structure")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles, Async")]
