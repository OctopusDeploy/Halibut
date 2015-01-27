using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Halibut.Diagnostics
{
    public static class ExceptionExtensions
    {
        public static Exception UnpackFromContainers(this Exception error)
        {
            var aggregateException = error as AggregateException;
            if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
            {
                return UnpackFromContainers(aggregateException.InnerExceptions[0]);
            }

            if (error is TargetInvocationException && error.InnerException != null)
            {
                return UnpackFromContainers(error.InnerException);
            }

            return error;
        }
    }
}
