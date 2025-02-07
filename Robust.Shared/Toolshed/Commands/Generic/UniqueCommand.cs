﻿using System.Collections.Generic;
using System.Linq;

namespace Robust.Shared.Toolshed.Commands.Generic;

[ToolshedCommand]
internal sealed class UniqueCommand : ToolshedCommand
{
    [CommandImplementation, TakesPipedTypeAsGeneric]
    public IEnumerable<T> Unique<T>([PipedArgument] IEnumerable<T> input)
        => input.Distinct();
}
