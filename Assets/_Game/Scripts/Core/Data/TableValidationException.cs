using System;
using System.Collections.Generic;

namespace Game.Core.Data
{
    /// One exception for every problem in the tables, so a bad commit is fixed in one pass.
    public sealed class TableValidationException : Exception
    {
        public TableValidationException(IReadOnlyList<string> problems)
            : base("Data tables failed validation (" + problems.Count + " problem(s)):\n - " + string.Join("\n - ", problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }
    }
}
