using System;
using System.Collections.Generic;

namespace HorseRacing.Domain.Exceptions
{
    public class LockConstraintException : Exception
    {
        public List<string> Blockers { get; }

        public LockConstraintException(List<string> blockers) 
            : base("Cannot lock user due to constraints.")
        {
            Blockers = blockers;
        }
    }
}
