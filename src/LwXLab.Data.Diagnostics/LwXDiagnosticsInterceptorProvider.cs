using System.Collections.Generic;
using LwXLab.Data.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LwXLab.Data.Diagnostics;

internal sealed class LwXDiagnosticsInterceptorProvider : ILwXDbContextInterceptorProvider
{
    private readonly LwXDataCommandInterceptor _interceptor;
    public LwXDiagnosticsInterceptorProvider(LwXDataCommandInterceptor interceptor) { _interceptor = interceptor; }
    public IEnumerable<IInterceptor> GetInterceptors(ILwXDbContextDescriptor descriptor)
    {
        yield return _interceptor;
    }
}
