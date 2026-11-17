using System;
using System.Collections.Generic;
using System.Text;

namespace OrderFlow.Application.Abstractions;

public interface IUnitOfWork
{

    Task SaveChangesAsync(CancellationToken cancellationToken);
    
}
