using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LwXLab.Data.EntityFrameworkCore;

/// <summary>Optional extension point used by diagnostics/safety packages to attach EF interceptors.</summary>
public interface ILwXDbContextInterceptorProvider
{
    IEnumerable<IInterceptor> GetInterceptors(ILwXDbContextDescriptor descriptor);
}
